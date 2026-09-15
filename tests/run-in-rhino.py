# Run using Rhino's _-RunPythonScript command. Uses a separate GH definition.
import os
import clr
import System
root = os.path.dirname(os.path.dirname(__file__))
clr.AddReferenceToFileAndPath(os.path.join(root, "dist", "Polymita.gha"))
test_assembly = System.Reflection.Assembly.LoadFrom(os.path.join(root, "test-output", "IntegrationTests3.dll"))
from Polymita import ShelfRuntime
ShelfRuntime.Initialize()
try:
    test_assembly.GetType("IntegrationTests").GetMethod("Run").Invoke(None, System.Array[System.Object]([root]))
except Exception as ex:
    print(str(ex))
else:
    print("Polymita: integration tests passed. See test-output/integration.txt")
