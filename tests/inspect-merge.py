import os
import clr
root = os.path.dirname(os.path.dirname(__file__))
clr.AddReferenceToFileAndPath(os.path.join(root, "dist", "Polymita.gha"))
from Grasshopper.Kernel import GH_Document
from Grasshopper.Kernel.Special import GH_Panel
from Polymita import Recipes, Insertion
from System.Drawing import PointF
lib = Recipes.Defaults()
item = [i for s in lib.Sections for i in s.Items if i.Name == u"Merge \u00b7 Flatten"][0]
doc = GH_Document()
p = GH_Panel()
p.CreateAttributes()
p.UserText = "0\r\n1"
doc.AddObject(p, False)
with open(os.path.join(root, "test-output", "merge-debug.txt"), "w") as f:
    obj = Recipes.Create(item)
    f.write("Before: " + str([(x.Name, str(x.DataMapping)) for x in obj.Params.Input]) + "\n")
    obj = Insertion.Insert(doc, item, PointF(200, 100), p, False, 1)
    f.write("After: " + str([(x.Name, str(x.DataMapping)) for x in obj.Params.Input]) + "\n")
doc.Dispose()
