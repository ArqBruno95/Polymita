import os
import clr
import System
root = os.path.dirname(os.path.dirname(__file__))
clr.AddReferenceToFileAndPath(os.path.join(root, "dist", "WireShelf.gha"))
from Grasshopper.Kernel.Special import GH_Panel
from WireShelf import Recipes
p = GH_Panel()
p.CreateAttributes()
p.UserText = "rojo\nverde\nazul"
p.NickName = "Materiales"
r = Recipes.Capture(p)
c = Recipes.Create(r)
with open(os.path.join(root, "test-output", "panel-debug.txt"), "w") as f:
    f.write("original text=" + repr(p.UserText) + "\nclone text=" + repr(c.UserText) + "\n")
    f.write("original name=" + repr(p.NickName) + "\nclone name=" + repr(c.NickName) + "\n")
    f.write(r.SnapshotXml)
