# Item icons for "Base Militar: Sobreviva a Noite".
# Flat glyph style: a light silhouette with cut-out details and a dark outline, drawn as signed
# distance fields on a 4x supersampled grid, so edges are clean at any size. Original artwork.
import numpy as np, zlib, struct, os, sys, math

OUT = sys.argv[1]
N = 256          # final size
SS = 4           # supersampling
G = N * SS
ys, xs = np.mgrid[0:G, 0:G].astype(np.float32)
X = (xs + 0.5) / G * 100.0     # work in a 0..100 space, y down
Y = (ys + 0.5) / G * 100.0

FILL = np.array([232, 230, 224], np.float32)
EDGE = np.array([12, 13, 15], np.float32)

# ---------------------------------------------------------------- sdf primitives
def circle(cx, cy, r):
    return np.hypot(X - cx, Y - cy) - r

def box(cx, cy, hw, hh, r=0.0, ang=0.0):
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    px, py = X - cx, Y - cy
    lx, ly = c * px + s * py, -s * px + c * py
    qx, qy = np.abs(lx) - (hw - r), np.abs(ly) - (hh - r)
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r

def capsule(x0, y0, x1, y1, r):
    dx, dy = x1 - x0, y1 - y0
    t = np.clip(((X - x0) * dx + (Y - y0) * dy) / (dx * dx + dy * dy), 0, 1)
    return np.hypot(X - (x0 + t * dx), Y - (y0 + t * dy)) - r

def poly(pts):
    pts = np.array(pts, np.float32)
    d = np.full(X.shape, 1e9, np.float32)
    sign = np.ones(X.shape, np.float32)
    n = len(pts)
    for i in range(n):
        ax, ay = pts[i]; bx, by = pts[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        wx, wy = X - ax, Y - ay
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        d = np.minimum(d, np.hypot(wx - ex * t, wy - ey * t))
        c1 = Y >= ay; c2 = Y < by; c3 = ex * wy > ey * wx
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        sign = np.where(flip, -sign, sign)
    return sign * d

def ring(cx, cy, r, w):
    return np.abs(circle(cx, cy, r)) - w

def U(*a):
    out = a[0]
    for b in a[1:]: out = np.minimum(out, b)
    return out

def S(a, *b):  # subtract
    for x in b: a = np.maximum(a, -x)
    return a

# ---------------------------------------------------------------- output
def png(path, rgba):
    h, w, _ = rgba.shape
    raw = b"".join(b"\x00" + rgba[y].tobytes() for y in range(h))
    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    open(path, "wb").write(data)

def render(name, sdf, stroke=2.4):
    px = 100.0 / G                                     # one supersample in sdf units
    fill = np.clip(0.5 - sdf / px, 0, 1)
    edge = np.clip(0.5 - (sdf - stroke) / px, 0, 1)    # silhouette grown by the stroke
    # soft top light: the fill is a touch brighter at the top
    shade = (1.0 - 0.10 * (Y / 100.0))[..., None]
    rgb = EDGE * (1 - fill[..., None]) + FILL * shade * fill[..., None]
    a = np.maximum(edge * 0.82, fill)
    rgba = np.dstack([rgb, a * 255.0])
    rgba = rgba.reshape(N, SS, N, SS, 4).mean(axis=(1, 3))
    png(os.path.join(OUT, name + ".png"), np.clip(rgba + 0.5, 0, 255).astype(np.uint8))
    print("icon", name)

# ---------------------------------------------------------------- icons
def medkit():
    body = box(50, 56, 38, 28, 6)
    handle = S(box(50, 26, 14, 8, 4), box(50, 28, 8, 5, 2))
    cross = U(box(50, 56, 15, 5, 1), box(50, 56, 5, 15, 1))
    return S(U(body, handle), cross)

def grenade():
    body = circle(46, 60, 26)
    grooves = U(box(46, 60, 26, 1.4), box(46, 60, 1.4, 26), box(46, 48, 26, 1.2), box(46, 72, 26, 1.2))
    top = box(46, 33, 9, 6, 2)
    lever = capsule(55, 31, 70, 52, 3.2)
    pin = ring(66, 26, 7, 2.2)
    return U(S(body, grooves), top, lever, pin)

def molotov():
    bottle = U(box(46, 66, 18, 24, 6), box(46, 36, 7, 12, 2))
    label = box(46, 68, 12, 9, 2)
    rag = poly([(40, 26), (52, 26), (50, 16), (44, 14)])
    flame = U(circle(50, 12, 7), poly([(43, 12), (57, 12), (50, -2)]), circle(60, 16, 4), poly([(56, 16), (64, 16), (63, 6)]))
    return U(S(bottle, label), rag, flame)

def flare():
    stick = box(42, 62, 9, 30, 3, -20)
    band = box(42, 62, 10, 3, 0, -20)
    cap = box(37, 37, 7, 5, 2, -20)
    sparks = U(circle(32, 22, 7), circle(24, 14, 3), circle(42, 12, 3), circle(20, 26, 2.5), circle(36, 6, 2))
    return U(S(stick, band), cap, sparks)

def barricade():
    p1 = box(50, 50, 40, 7, 1.5, 28)
    p2 = box(50, 50, 40, 7, 1.5, -28)
    post = U(box(18, 54, 5, 34, 1.5), box(82, 54, 5, 34, 1.5))
    nails = U(circle(18, 38, 1.8), circle(82, 38, 1.8), circle(18, 70, 1.8), circle(82, 70, 1.8))
    return S(U(p1, p2, post), nails)

def decoy():
    body = box(42, 62, 22, 18, 4)
    grill = U(*[box(36, 56 + i * 6, 12, 1.4) for i in range(3)])
    lamp = circle(56, 56, 4)
    antenna = capsule(56, 44, 66, 18, 2.2)
    waves = U(S(ring(70, 26, 10, 2.2), box(60, 26, 10, 14)), S(ring(70, 26, 18, 2.2), box(58, 26, 12, 22)))
    return U(S(body, grill, lamp), antenna, waves)

def fuse():
    a = math.radians(35)
    ax, ay = -math.sin(a), math.cos(a)            # the fuse's long axis
    glass = box(50, 50, 12, 22, 4, 35)
    caps = U(box(50 + ax * 24, 50 + ay * 24, 14, 6, 1.5, 35), box(50 - ax * 24, 50 - ay * 24, 14, 6, 1.5, 35))
    tips = U(capsule(50 + ax * 30, 50 + ay * 30, 50 + ax * 38, 50 + ay * 38, 3), capsule(50 - ax * 30, 50 - ay * 30, 50 - ax * 38, 50 - ay * 38, 3))
    wire = box(50, 50, 1.6, 15, 0, 35)
    return U(S(glass, wire), caps, tips)

def module():
    board = box(50, 52, 36, 28, 4)
    chip = box(46, 52, 12, 12, 1.5)
    traces = U(box(70, 40, 6, 1.4), box(70, 52, 6, 1.4), box(70, 64, 6, 1.4), box(24, 44, 6, 1.4), box(24, 60, 6, 1.4))
    holes = U(circle(20, 30, 2.4), circle(80, 30, 2.4), circle(20, 74, 2.4), circle(80, 74, 2.4))
    pins = U(*[box(30 + i * 8, 86, 2, 6, 0.5) for i in range(6)])
    return U(S(board, chip, traces, holes), box(46, 52, 8, 8, 1), pins)

def keycard():
    card = box(50, 52, 40, 26, 5, -8)
    chip = box(32, 50, 9, 7, 1.5, -8)
    stripe = box(56, 34, 26, 3, 1, -8)
    lines = U(box(60, 56, 18, 2, 1, -8), box(56, 66, 14, 2, 1, -8))
    hole = box(78, 32, 5, 2, 1, -8)
    return S(card, chip, stripe, lines, hole)

def document():
    page = S(box(48, 52, 28, 38, 2), poly([(62, 13), (77, 13), (77, 28)]))
    fold = poly([(62, 14), (62, 28), (76, 28)])
    lines = U(*[box(44, 36 + i * 9, 18 if i % 3 else 14, 2, 1) for i in range(6)])
    return U(S(page, lines), fold)

def codes():
    page = box(46, 52, 28, 38, 2)
    bars = U(*[box(30 + i * 3.4, 72, 0.8 + (i % 3) * 0.5, 9) for i in range(10)])
    stamp = ring(46, 38, 11, 2.2)
    star = poly([(46, 30), (49, 37), (56, 37), (50, 41), (52, 48), (46, 44), (40, 48), (42, 41), (36, 37), (43, 37)])
    return S(page, bars, stamp, star)

def blackbox():
    body = box(50, 58, 34, 26, 4)
    band = box(50, 76, 34, 6)
    stripes = np.maximum(U(*[box(14 + i * 10, 76, 2.4, 9, 0, 35) for i in range(8)]), band)
    handle = S(box(50, 26, 16, 8, 4), box(50, 28, 10, 5, 2))
    plate = box(50, 50, 18, 9, 2)
    bolts = U(circle(22, 40, 2), circle(78, 40, 2))
    return U(S(body, stripes, plate, bolts), handle, box(50, 50, 13, 2, 1))

def case():
    body = box(50, 58, 40, 24, 5)
    seam = box(50, 52, 40, 1.6)
    latches = U(box(30, 52, 4, 5, 1), box(70, 52, 4, 5, 1))
    handle = S(box(50, 30, 14, 7, 4), box(50, 32, 8, 4, 2))
    return U(S(body, seam), latches, handle)

def bullet(cx, top, w, h):
    tip = U(circle(cx, top + w, w), box(cx, top + w + 3, w, 3))
    casing = box(cx, top + w + 3 + h / 2, w * 1.08, h / 2, 1)
    groove = box(cx, top + w + 3 + 2.5, w * 1.1, 0.9)
    rim = box(cx, top + w + 3 + h - 1.5, w * 1.18, 2, 0.8)
    return S(U(tip, casing, rim), groove)

def ammo_pistol():
    return U(bullet(36, 26, 8, 40), bullet(62, 34, 8, 40))

def ammo_rifle():
    return U(bullet(30, 14, 6, 62), bullet(50, 18, 6, 62), bullet(70, 22, 6, 62))

def ammo_sniper():
    return bullet(50, 6, 9, 72)

def ammo_shotgun():
    def shell(cx, top):
        hull = box(cx, top + 26, 11, 26, 3)
        brass = box(cx, top + 48, 12, 8, 1.5)
        ribs = U(*[box(cx, top + 10 + i * 6, 11, 0.9) for i in range(4)])
        return U(S(hull, ribs), brass)
    return U(shell(36, 18), shell(64, 26))

def ammo_box():
    body = box(50, 60, 38, 24, 3)
    lid = box(50, 34, 40, 5, 2)
    handle = S(box(50, 24, 10, 5, 2.5), box(50, 26, 6, 3, 1.5))
    label = box(50, 62, 20, 10, 2)
    rounds = U(bullet(42, 54, 3, 8), bullet(50, 54, 3, 8), bullet(58, 54, 3, 8))
    return U(S(body, label), lid, handle, rounds)

def credits():
    coin = circle(50, 50, 36)
    inner = ring(50, 50, 29, 1.8)
    # military chevrons
    def chevron(y):
        return S(poly([(30, y + 8), (50, y - 6), (70, y + 8), (70, y + 16), (50, y + 2), (30, y + 16)]), box(50, 100, 1, 1))
    return S(coin, inner, chevron(32), chevron(48))

def health():
    return U(box(50, 50, 32, 11, 3), box(50, 50, 11, 32, 3))

def armor():
    vest = poly([(26, 18), (40, 18), (44, 26), (56, 26), (60, 18), (74, 18), (80, 34), (76, 86), (24, 86), (20, 34)])
    neck = circle(50, 18, 9)
    plates = U(box(36, 56, 8, 14, 2), box(64, 56, 8, 14, 2), box(50, 76, 22, 3, 1))
    return S(vest, neck, plates)

def upgrade():
    # a wrench across a gear
    gear = S(U(circle(42, 58, 22), *[box(42 + 26 * math.cos(math.radians(k * 45)), 58 + 26 * math.sin(math.radians(k * 45)), 6, 6, 1.5, k * 45) for k in range(8)]), circle(42, 58, 9))
    handle = box(66, 34, 6, 26, 3, 45)
    head = S(circle(80, 20, 13), box(86, 14, 6, 7, 0, 45))
    return U(gear, handle, head)

def stamina():
    # a lightning bolt
    return poly([(56, 8), (28, 54), (46, 54), (38, 92), (72, 40), (54, 40), (64, 8)])

def capacity():
    # a backpack with a plus
    bag = U(box(50, 58, 28, 32, 9), box(50, 30, 16, 8, 4))
    straps = U(box(40, 26, 3, 8), box(60, 26, 3, 8))
    pocket = box(50, 70, 18, 12, 4)
    plus = U(box(50, 52, 9, 2.6, 1), box(50, 52, 2.6, 9, 1))
    return U(S(bag, pocket, plus), straps)

# ---------------------------------------------------------------- crafting materials and products
def scrap():
    # bent sheet metal with rivets
    sheet = poly([(18, 34), (62, 22), (84, 44), (74, 78), (30, 82), (14, 60)])
    tear = poly([(46, 52), (60, 44), (56, 62)])
    rivets = U(circle(28, 42, 2.6), circle(60, 32, 2.6), circle(72, 70, 2.6), circle(32, 72, 2.6))
    return S(sheet, tear, rivets)

def cloth():
    # a folded roll of fabric
    roll = box(46, 50, 30, 20, 8)
    end = circle(76, 50, 16)
    spiral = U(ring(76, 50, 9, 1.6), circle(76, 50, 2.4))
    tail = poly([(20, 66), (60, 66), (66, 88), (26, 88)])
    seam = box(44, 77, 16, 1.4)
    return U(S(roll, box(50, 50, 30, 1.4)), S(end, spiral), S(tail, seam))

def electronics():
    # a chip with legs
    body = box(50, 50, 24, 24, 3)
    dot = circle(38, 38, 3.2)
    legs = U(*[box(26 + i * 9.6, 18, 2.2, 8, 0.6) for i in range(6)], *[box(26 + i * 9.6, 82, 2.2, 8, 0.6) for i in range(6)])
    core = box(54, 54, 10, 10, 1.5)
    return U(S(body, dot, ring(54, 54, 10, 1.4)), legs, S(core, box(54, 54, 6, 6, 1)))

def med_supplies():
    # a bottle with a cross and a syringe
    bottle = U(box(38, 62, 18, 26, 4), box(38, 30, 10, 6, 2))
    cross = U(box(38, 64, 9, 3, 0.6), box(38, 64, 3, 9, 0.6))
    barrel = box(70, 52, 7, 22, 2, 30)
    needle = capsule(78, 32, 84, 20, 1.4)
    plunger = box(60, 72, 8, 3, 1, 30)
    return U(S(bottle, cross), S(barrel, box(70, 52, 3, 14, 0.6, 30)), needle, plunger)

def mech_parts():
    # a gear and a bolt
    gear = S(U(circle(40, 46, 20), *[box(40 + 23 * math.cos(math.radians(k * 45)), 46 + 23 * math.sin(math.radians(k * 45)), 5, 5, 1.2, k * 45) for k in range(8)]), circle(40, 46, 8))
    head = poly([(66, 60), (78, 60), (84, 70), (78, 80), (66, 80), (60, 70)])
    shaft = box(72, 88, 4, 8, 1)
    return U(gear, S(head, circle(72, 70, 3)), shaft)

def chemicals():
    # a flask with a level line
    neck = box(50, 22, 7, 12, 2)
    flask = poly([(40, 30), (60, 30), (80, 82), (20, 82)])
    liquid = poly([(30, 58), (70, 58), (80, 82), (20, 82)])
    bubbles = U(circle(44, 70, 3), circle(56, 66, 2.2), circle(52, 76, 2))
    return U(neck, S(flask, box(50, 54, 20, 1.6)), S(liquid, bubbles))

def wiring():
    # a coil of wire with a plug
    coil = U(ring(44, 54, 26, 3), ring(44, 54, 17, 3), ring(44, 54, 8, 3))
    lead = capsule(68, 42, 82, 22, 3)
    plug = box(84, 16, 6, 8, 1.5, 30)
    return U(coil, lead, plug)

def tactical_parts():
    # a rail section with a clamp
    rail = box(50, 54, 40, 9, 1.5)
    slots = U(*[box(18 + i * 9.4, 50, 2.2, 5, 0.4) for i in range(8)])
    clamp = U(box(50, 70, 14, 7, 2), box(50, 38, 8, 6, 1.5))
    screw = circle(50, 70, 3)
    return U(S(rail, slots), S(clamp, screw))

def bandage():
    # a rolled bandage with a tail
    roll = circle(40, 46, 26)
    core = circle(40, 46, 9)
    layers = ring(40, 46, 18, 1.4)
    tail = box(66, 72, 22, 9, 2, -20)
    stripes = U(box(62, 70, 1.4, 9, 0, -20), box(72, 74, 1.4, 9, 0, -20))
    return U(S(roll, core, layers), S(tail, stripes))

def workbench():
    # a bench with a vice and a hammer
    top = box(50, 52, 42, 6, 1.5)
    legs = U(box(16, 74, 4, 18, 1), box(84, 74, 4, 18, 1))
    brace = box(50, 80, 34, 2.4)
    vice = U(box(26, 40, 8, 6, 1.5), box(26, 32, 12, 3, 1))
    handle = box(64, 34, 3, 14, 1, 40)
    head = box(72, 24, 9, 4, 1, 40)
    return U(top, legs, brace, vice, handle, head)

def vest_patch():
    # the vest with a plate being added
    vest = poly([(26, 18), (40, 18), (44, 26), (56, 26), (60, 18), (74, 18), (80, 34), (76, 86), (24, 86), (20, 34)])
    neck = circle(50, 18, 9)
    plate = box(50, 60, 16, 18, 3)
    plus = U(box(50, 60, 8, 2.4, 1), box(50, 60, 2.4, 8, 1))
    return U(S(vest, neck, box(50, 60, 19, 21, 4)), S(plate, plus))

def supply_pack():
    # a crate with straps and a parachute ring
    crate = box(50, 60, 34, 26, 3)
    straps = U(box(36, 60, 3, 26), box(64, 60, 3, 26))
    plank = box(50, 60, 34, 1.6)
    loop = S(ring(50, 24, 10, 2.6), box(50, 32, 12, 4))
    return U(S(crate, straps, plank), box(36, 60, 1.4, 26), box(64, 60, 1.4, 26), loop, capsule(50, 34, 50, 38, 2))

def unknown():
    q = S(ring(50, 38, 15, 5), box(36, 46, 9, 12))
    return U(S(box(50, 50, 38, 38, 8), q, capsule(50, 54, 50, 62, 4.5), circle(50, 76, 5)))

ICONS = {
    "medkit": medkit, "grenade": grenade, "molotov": molotov, "flare": flare, "barricade": barricade, "decoy": decoy,
    "fuse": fuse, "radio_module": module, "keycard": keycard, "document": document, "evac_codes": codes,
    "blackbox": blackbox, "case": case,
    "ammo_pistol": ammo_pistol, "ammo_rifle": ammo_rifle, "ammo_sniper": ammo_sniper, "ammo_shotgun": ammo_shotgun, "ammo_box": ammo_box,
    "credits": credits, "health": health, "armor": armor, "unknown": unknown,
    "upgrade": upgrade, "stamina": stamina, "capacity": capacity,
    "scrap": scrap, "cloth": cloth, "electronics": electronics, "med_supplies": med_supplies, "mech_parts": mech_parts,
    "chemicals": chemicals, "wiring": wiring, "tactical_parts": tactical_parts,
    "bandage": bandage, "workbench": workbench, "vest_patch": vest_patch, "supply_pack": supply_pack,
}

os.makedirs(OUT, exist_ok=True)
only = sys.argv[2:]
for name, fn in ICONS.items():
    if only and name not in only: continue
    render(name, fn())
