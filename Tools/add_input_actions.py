import json, io, uuid, sys
path = sys.argv[1]
raw = io.open(path, encoding="utf-8-sig").read()
d = json.loads(raw)
m = [x for x in d["maps"] if x["name"] == "Player"][0]
names = {a["name"] for a in m["actions"]}
def action(name, kind):
    return {"name": name, "type": kind, "id": str(uuid.uuid4()), "expectedControlType": "Button",
            "processors": "", "interactions": "", "initialStateCheck": False}
def binding(action, path, group):
    return {"name": "", "id": str(uuid.uuid4()), "path": path, "interactions": "", "processors": "",
            "groups": group, "action": action, "isComposite": False, "isPartOfComposite": False}
kb = [b["groups"] for b in m["bindings"] if b["path"] == "<Keyboard>/e"][0]
gp = [b["groups"] for b in m["bindings"] if b["path"] == "<Gamepad>/buttonNorth"][0]
add = [("Prone", "PassThrough", [("<Keyboard>/c", kb), ("<Gamepad>/dpad/up", gp)]), ("Flashlight", "Button", [("<Keyboard>/f", kb)]),
       # contextual: Q/E and the shoulder buttons only lean while aiming
       ("LeanLeft", "PassThrough", [("<Keyboard>/q", kb), ("<Gamepad>/leftShoulder", gp)]),
       ("LeanRight", "PassThrough", [("<Keyboard>/e", kb), ("<Gamepad>/rightShoulder", gp)])]
for name, kind, binds in add:
    if name in names: continue
    m["actions"].append(action(name, kind))
    for p, g in binds: m["bindings"].append(binding(name, p, g))
io.open(path, "w", encoding="utf-8").write(json.dumps(d, indent=4, ensure_ascii=False))
print("ok", [a["name"] for a in m["actions"]][-4:])
