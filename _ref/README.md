# Reference material

This folder holds third-party code used **only as a protocol reference** while writing
`src/ZandronumWatcher.Core/Protocol`. Nothing here is compiled into the app.

## pyzandro

`pyzandro/` is vendored from [Trillster/pyzandro](https://github.com/Trillster/pyzandro),
MIT licensed (see `pyzandro/LICENSE.txt`, Copyright (c) 2023 klaufir). It is kept in-tree
because it documents the exact byte order of the master-server and launcher responses,
which the C# port in `Protocol/` mirrors field for field.

The Huffman table in `src/ZandronumWatcher.Core/Protocol/HuffmanTables.g.cs` is generated
from `pyzandro/huffman.py` by `tools/generate-huffman-tables.py`.
