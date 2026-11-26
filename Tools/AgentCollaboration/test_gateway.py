"""Boundary tests for the task gateway; run with the collaboration venv."""
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch


class GatewayBoundaries(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / 'worktree'
        self.root.mkdir()
        policy = Path(self.temp.name) / 'policy.json'
        policy.write_text(json.dumps({'worktree': str(self.root), 'write_dirs': ['Task'], 'audit': str(Path(self.temp.name) / 'audit.jsonl')}))
        spec = importlib.util.spec_from_file_location('task_gateway_test', Path(__file__).with_name('gateway.py'))
        self.gateway = importlib.util.module_from_spec(spec)
        with patch.object(sys, 'argv', ['gateway.py', str(policy)]):
            spec.loader.exec_module(self.gateway)

    def tearDown(self):
        self.temp.cleanup()

    def test_write_read_validate(self):
        self.gateway.write_file('Task/result.json', '{"message":"Проверка"}\n')
        self.assertIn('Проверка', self.gateway.read_file('Task/result.json'))
        self.assertTrue(self.gateway.run_check('json', 'Task/result.json')['valid'])

    def test_outside_scope(self):
        for name in ['README.md', '../escape', 'Task/../../escape', 'Task/../escape', 'C:/Windows/win.ini', 'Task/file:stream', '.git/config', 'Task/.kimi-code/mcp.json']:
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.gateway.write_file(name, 'blocked')

    def test_no_arbitrary_command(self):
        for name in ['git_merge', 'powershell', 'git push', 'python']:
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.gateway.run_check(name)

    def test_subprocess_has_no_proxy_or_mcp_stdin(self):
        with patch.dict(os.environ, {'HTTP_PROXY': 'http://127.0.0.1:7890', 'NO_PROXY': '::1,[::1]'}):
            result = self.gateway.command_run([sys.executable, '-c',
                'import os,sys; print(bool(os.environ.get("HTTP_PROXY"))); print(repr(sys.stdin.read()))'])
        self.assertEqual(0, result.returncode)
        self.assertEqual(["False", "''"], result.stdout.splitlines())

    @unittest.skipUnless(os.name == 'nt', 'Windows process-tree timeout test')
    def test_timeout_stops_descendants(self):
        import psutil
        pidfile = Path(self.temp.name) / 'child.pid'
        script = ('import subprocess,sys,time,pathlib; '
                  'p=subprocess.Popen([sys.executable,"-c","import time; time.sleep(60)"]); '
                  f'pathlib.Path({str(pidfile)!r}).write_text(str(p.pid)); time.sleep(60)')
        with self.assertRaises(TimeoutError):
            self.gateway.command_run([sys.executable, '-c', script], timeout=2)
        self.assertTrue(pidfile.exists())
        self.assertFalse(psutil.pid_exists(int(pidfile.read_text())))

    def test_no_github_write_or_arbitrary_repo(self):
        for name in ['/issues', '/../../another/repo', 'https://example.com']:
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.gateway.github_read(name)

    def test_hardlink_rejected(self):
        original = Path(self.temp.name) / 'original.txt'
        original.write_text('protected')
        (self.root / 'Task').mkdir()
        os.link(original, self.root / 'Task/link.txt')
        with self.assertRaises(ValueError):
            self.gateway.write_file('Task/link.txt', 'changed')
        self.assertEqual('protected', original.read_text())

    def test_symlink_rejected(self):
        target = Path(self.temp.name) / 'outside'
        target.mkdir()
        try:
            (self.root / 'Task').symlink_to(target, target_is_directory=True)
        except OSError:
            self.skipTest('Windows symlink privilege unavailable')
        with self.assertRaises(ValueError):
            self.gateway.write_file('Task/test.txt', 'changed')

    @unittest.skipUnless(os.name == 'nt', 'Windows junction test')
    def test_junction_rejected(self):
        import _winapi
        target = Path(self.temp.name) / 'outside'
        target.mkdir()
        _winapi.CreateJunction(str(target), str(self.root / 'Task'))
        with self.assertRaises(ValueError):
            self.gateway.write_file('Task/test.txt', 'changed')
        self.assertFalse((target / 'test.txt').exists())


if __name__ == '__main__':
    unittest.main()
