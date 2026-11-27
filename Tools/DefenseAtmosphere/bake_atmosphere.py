"""Bake original RAID defense PCM sound cues."""
from array import array
from math import cos, exp, pi, sin, tanh
from pathlib import Path
import random
import sys
import wave


ROOT = Path(__file__).resolve().parents[2] / "Assets/_Game/Nereid/DefenseAtmosphere/Resources/DefenseAtmosphere"
HZ = 24_000
TAU = 2 * pi


def envelope(t, attack, decay):
    return (1 - exp(-t / attack)) * exp(-t / decay)


def voice(t, f, wobble=0, rate=4):
    return sin(TAU * f * t + wobble * sin(TAU * rate * t))


def pulse(t, center, width):
    return exp(-((t - center) / width) ** 2)


def write_sound(name, duration, synth, *, seamless=False):
    output = ROOT / "Audio" / f"{name}.wav"
    output.parent.mkdir(parents=True, exist_ok=True)
    rng = random.Random(name)
    samples = array("h")
    low = 0
    for n in range(round(HZ * duration)):
        time = n / HZ
        noise = rng.uniform(-1, 1)
        low += (noise - low) * .047
        taper = 1 if seamless else min(1, (duration - time) * 22, time * 95)
        samples.append(round(30_900 * tanh(synth(time, noise, low)) * taper))
    if sys.byteorder != "little":
        samples.byteswap()
    with wave.open(str(output), "wb") as file:
        file.setparams((1, 2, HZ, 0, "NONE", "not compressed"))
        file.writeframes(samples.tobytes())


def roar(t, _, mud):
    drift = 168 * (1 - exp(-t * .29)) + 30 * t
    motor = .61 * sin(TAU * drift + .38 * sin(TAU * 5.2 * t))
    motor += .24 * sin(TAU * drift * 2.09 + sin(TAU * 7.4 * t))
    motor += .11 * voice(t, 166, .65, 12.6)
    rib = .69 + .18 * sin(TAU * 7.3 * t) + .08 * sin(TAU * 17.8 * t)
    return (.71 * motor * rib + .52 * mud) * envelope(t, .24, 1.08) * (1 + .38 * pulse(t, 1.08, .26))


def theme(t, _, __):
    bar = TAU * t / 14
    def pitch(multiple, modulation=0):
        return sin(bar * multiple + modulation * sin(bar))

    # Integer cycles and a periodic envelope make the score loop sample-accurately.
    bow = pitch(364, .54) * .24 + pitch(546, -.82) * .1 + pitch(728, .27) * .065
    hull = pitch(573, .31) * .072 + pitch(841, -.48) * .052
    hammer = (.5 + .5 * cos(bar * 9 + .6)) ** 7 * pitch(490, .51) * .14
    return bow * (.78 + .17 * cos(2 * bar)) + hull * (.51 + .44 * sin(3 * bar)) + hammer


def impact(t, white, mud):
    body = .55 * sin(TAU * (38 * t + 18 * (1 - exp(-t * 11)) / 11))
    ring = .24 * voice(t, 283, .34, 9) + .17 * voice(t, 421, .73, 7) + .08 * voice(t, 990)
    return body * envelope(t, .012, .162) + ring * envelope(t, .003, .26) + (white * .23 + mud * .96) * envelope(t, .013, .075)


def fracture(t, white, mud):
    collapse = .7 * sin(TAU * (37 * t + 4 * (1 - exp(-t * 6))))
    shards = (.2 * white + .65 * mud + .16 * voice(t, 872, 3, 13)) * envelope(t, .013, .103)
    return collapse * envelope(t, .03, .56) + shards + .3 * voice(t, 413, 3.7, 33) * envelope(t, .04, .16)


def creature_cue(kind, ability):
    def play(t, white, mud):
        if kind == 0:
            return (voice(t, 55 if ability else 39, 2.7, 6.2) * .63 + mud * 1.15) * envelope(t, .08 if ability else .015, .21)
        if kind == 1:
            pull = voice(t, 165, 3.5 * exp(-t * 1.7), 14) * .43 + mud * 1.25
            return pull * envelope(t, .034, .27) + white * .32 * pulse(t, .17 if ability else .047, .017)
        if kind == 2:
            snaps = pulse(t, .11, .026) + .72 * pulse(t, .24, .014)
            return (voice(t, 584 if ability else 437, .74, 20) * .39 + white * .22 + mud * .67) * snaps
        if kind == 3:
            phase = TAU * (124 * t + 38 * t * t)
            tentacles = sin(phase) * .35 + sin(phase * 2.37) * .14 + mud * .72
            return tentacles * envelope(t, .13 if ability else .031, .34) * (.7 + .3 * cos(t * TAU * 8))
        screech = voice(t, 373 if ability else 210, 2.9, .97) * .47 + voice(t, 554, 1.1, 8) * .1
        return screech * envelope(t, .021, .20) + mud * 1.04 * pulse(t, .19, .102)
    return play


def sound_design():
    write_sound("HullRoar", 2.65, roar)
    write_sound("DistantRoar", 2.2, lambda t, _, m: (
        .54 * voice(t, 44, 1.9, .45) + .29 * voice(t, 77, .67, 3.1)
        + .14 * voice(t, 105, 1.13, 6.4) + .61 * m) * envelope(t, .22, .88))
    write_sound("RingPressure", 14, theme, seamless=True)
    write_sound("FieldHum", 4, lambda t, _, __: (
        sin(TAU * 211 * t / 4) * .28 + sin(TAU * 422 * t / 4) * .092
        + sin(TAU * 553 * t / 4 + sin(TAU * t / 4)) * .055) * (.85 + .145 * sin(TAU * t / 4)), seamless=True)
    write_sound("DarkPulse", .83, lambda t, _, m: (
        .51 * voice(t, 94, .87, 6.4) + .27 * voice(t, 153, 1.35, 2.7) + m * 1.15) * envelope(t, .16, .41))
    write_sound("DarkClick", .48, lambda t, w, m: (.16*w + 1.2*m + .4 * voice(t, 504)) * (
        pulse(t, .09, .009) + .51 * pulse(t, .21, .018) + .33 * pulse(t, .29, .013)))
    write_sound("DarkCall", .84, lambda t, _, m: (
        .5 * voice(t, 217, 3.2, 3.1) + .19 * voice(t, 456, 2) + .44 * m) * envelope(t, .102, .32))
    write_sound("FieldImpact", .8, impact)
    write_sound("FieldCritical", 1.18, lambda t, _, m: (
        .57 * voice(t, 142, 1, 7) + .25 * voice(t, 223, .38, 5) + .68 * m) *
        (.91 * pulse(t, .20, .063) + .79 * pulse(t, .52, .057) + .46 * pulse(t, .79, .079)))
    write_sound("FieldBreak", 1.55, fracture)
    write_sound("FieldRestore", 1.6, lambda t, _, m: (
        .31 * sin(TAU * (88*t + 193*t*t/2)) + .12 * sin(TAU * (134*t + 146*t*t)) + .4*m) * envelope(t, .37, .67))
    for kind, title in enumerate(("Rammer", "Remora", "Thief", "Abductor", "Stalker")):
        write_sound(f"{title}Strike", .65, creature_cue(kind, False))
        write_sound(f"{title}Ability", .84, creature_cue(kind, True))


if __name__ == "__main__":
    sound_design()
