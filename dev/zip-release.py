import hashlib
import os
import sys
import zipfile

source, target, prefix, *executables = sys.argv[1:]
if os.path.exists(target):
    os.remove(target)
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
    for folder, _, files in os.walk(source):
        for name in sorted(files):
            path = os.path.join(folder, name)
            relative = os.path.relpath(path, source).replace(os.sep, "/")
            info = zipfile.ZipInfo.from_file(path, f"{prefix}/{relative}")
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = (0o755 if relative in executables else 0o644) << 16
            with open(path, "rb") as stream:
                archive.writestr(info, stream.read())

digest = hashlib.sha256(open(target, "rb").read()).hexdigest().upper()
line = f"{digest}  {os.path.basename(target)}"
with open(target + ".sha256", "w") as checksum:
    checksum.write(line + "\n")
print(line)
