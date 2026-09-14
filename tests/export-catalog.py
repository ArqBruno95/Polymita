import os
import json
import codecs
from Grasshopper import Instances
root = os.path.dirname(os.path.dirname(__file__))
libraries = dict((str(x.Id), x.Name) for x in Instances.ComponentServer.Libraries)
catalog = []
for p in Instances.ComponentServer.ObjectProxies:
    catalog.append(dict(id=str(p.Guid), name=p.Desc.Name, nickname=p.Desc.NickName,
        category=p.Desc.Category, subcategory=p.Desc.SubCategory, description=p.Desc.Description,
        library=libraries.get(str(p.LibraryGuid), str(p.LibraryGuid)), obsolete=p.Obsolete))
with codecs.open(os.path.join(root, 'test-output', 'installed-catalog.json'), 'w', 'utf-8') as f:
    json.dump(catalog, f, ensure_ascii=False, indent=2)
print('WireShelf: exported %s installed components.' % len(catalog))
