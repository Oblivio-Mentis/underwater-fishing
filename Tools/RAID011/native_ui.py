"""Windows-only native mouse/keyboard verification through the game's real EventSystem."""
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import time
from probe import send, state, summary


class NativeUi:
    def __init__(self, process_id, directory):
        self.directory = Path(directory)
        self.user = ctypes.WinDLL("user32", use_last_error=True)
        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self.user.GetForegroundWindow.restype = wintypes.HWND
        self.user.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
        self.user.SetForegroundWindow.argtypes = [wintypes.HWND]
        self.user.BringWindowToTop.argtypes = [wintypes.HWND]
        self.user.ClientToScreen.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.POINT)]
        self.user.GetClientRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
        self.user.SetCursorPos.argtypes = [ctypes.c_int, ctypes.c_int]
        self.user.mouse_event.argtypes = [wintypes.DWORD,wintypes.DWORD,wintypes.DWORD,wintypes.DWORD,ctypes.c_size_t]
        self.user.mouse_event.restype = None
        self.user.keybd_event.argtypes = [wintypes.BYTE,wintypes.BYTE,wintypes.DWORD,ctypes.c_size_t]
        self.user.keybd_event.restype = None
        self.handle = None
        callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        def inspect(handle, _):
            owner = wintypes.DWORD()
            self.user.GetWindowThreadProcessId(handle, ctypes.byref(owner))
            if owner.value == process_id and self.user.IsWindowVisible(handle):
                self.handle = handle
                return False
            return True
        self.user.EnumWindows(callback_type(inspect), 0)
        if not self.handle:
            raise RuntimeError("Target game window not found")

    def current(self):
        return state(self.directory)

    def focus(self):
        owner = wintypes.DWORD()
        thread = self.user.GetWindowThreadProcessId(self.user.GetForegroundWindow(), ctypes.byref(owner))
        current = self.kernel.GetCurrentThreadId()
        self.user.AttachThreadInput(current, thread, True)
        try:
            self.user.BringWindowToTop(self.handle)
            self.user.SetForegroundWindow(self.handle)
        finally:
            self.user.AttachThreadInput(current, thread, False)
        if self.user.GetForegroundWindow() != self.handle:
            raise RuntimeError("Target game window did not receive focus")
        time.sleep(0.12)

    def move_pointer(self, x, y):
        rect = wintypes.RECT()
        self.user.GetClientRect(self.handle, ctypes.byref(rect))
        if not (0 <= x < rect.right and 0 <= y < rect.bottom):
            raise ValueError("UI target lies outside game client area")
        point = wintypes.POINT(round(x), round(rect.bottom - y))
        self.user.ClientToScreen(self.handle, ctypes.byref(point))
        self.user.SetCursorPos(point.x, point.y)
        self.last_screen_point=(point.x,point.y)
        actual=wintypes.POINT()
        self.user.GetCursorPos(ctypes.byref(actual))
        if (actual.x,actual.y)!=(point.x,point.y):
            raise RuntimeError("Pointer target "+str((point.x,point.y))+" actual "+str((actual.x,actual.y))+"; page="+self.current()["page"]+" cursor="+self.current()["cursor"])

    def pointer_click(self, x, y):
        self.focus()
        self.move_pointer(x, y)
        if self.user.GetForegroundWindow() != self.handle:
            raise RuntimeError("Focus changed before click")
        self.user.mouse_event(2, 0, 0, 0, 0)
        time.sleep(0.08)
        self.user.SetCursorPos(*self.last_screen_point)
        self.user.mouse_event(4, 0, 0, 0, 0)
        time.sleep(0.3)
        return self.current()

    def click(self, name):
        current = self.current()
        button = next((b for b in current["controls"] if b["name"] == name and b["interactable"]), None)
        if button is None:
            raise RuntimeError("Interactable UI button missing: " + name)
        return self.pointer_click(button["x"] + button["w"] / 2, button["y"] + button["h"] / 2)

    def key(self, virtual_key, seconds=0.08):
        self.focus()
        scan=self.user.MapVirtualKeyW(virtual_key,0)&0xff
        self.user.keybd_event(virtual_key, scan, 0, 0)
        try:
            time.sleep(seconds)
        finally:
            self.user.keybd_event(virtual_key, scan, 2, 0)
        time.sleep(0.25)
        return self.current()

    def look(self, dx, dy):
        self.focus()
        self.user.mouse_event(1, ctypes.c_uint32(round(dx)).value, ctypes.c_uint32(round(dy)).value, 0, 0)
        time.sleep(0.2)
        return self.current()

    def reel(self, held):
        self.focus()
        self.user.mouse_event(2 if held else 4, 0, 0, 0, 0)

    def scroll(self, ticks):
        self.focus()
        current = self.current()
        self.move_pointer(current["width"] / 2, current["height"] / 2)
        self.user.mouse_event(0x0800, 0, 0, ctypes.c_uint32(ticks * 120).value, 0)
        time.sleep(0.35)
        return self.current()

    def wait(self, predicate, timeout=10):
        until = time.monotonic() + timeout
        while time.monotonic() < until:
            current = self.current()
            if predicate(current):
                return current
            time.sleep(0.1)
        raise TimeoutError(json.dumps(summary(self.current()), ensure_ascii=False))

    def capture(self, filename):
        send(self.directory, "capture", value=filename)
        time.sleep(0.2)

    def resume(self):
        self.focus()
        if self.current()["page"] == "Pause":
            self.click("PauseResume")
        time.sleep(0.25)
        return self.current()
