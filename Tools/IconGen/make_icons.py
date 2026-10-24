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

def unknown():
    q = S(ring(50, 38, 15, 5), box(36, 46, 9, 12))
    return U(S(box(50, 50, 38, 38, 8), q, capsule(50, 54, 50, 62, 4.5), circle(50, 76, 5)))

ICONS = {
    "medkit": medkit, "grenade": grenade, "molotov": molotov, "flare": flare, "barricade": barricade, "decoy": decoy,
    "fuse": fuse, "radio_module": module, "keycard": keycard, "document": document, "evac_codes": codes,
    "blackbox": blackbox, "case": case,
    "ammo_pistol": ammo_pistol, "ammo_rifle": ammo_rifle, "ammo_sniper": ammo_sniper, "ammo_shotgun": ammo_shotgun, "ammo_box": ammo_box,
    "credits": credits, "health": health, "armor": armor, "unknown": unknown,
}

os.makedirs(OUT, exist_ok=True)
only = sys.argv[2:]
for name, fn in ICONS.items():
    if only and name not in only: continue
    render(name, fn())
