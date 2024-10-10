import argparse
import os
import pathlib
import re
import subprocess

FLATC_PATH = "./bin/flatc"
FLATC_URL_LINUX = "https://github.com/google/flatbuffers/releases/download/v24.3.25/Linux.flatc.binary.g++-13.zip"
FLATC_URL_WINDOWS = "https://github.com/google/flatbuffers/releases/download/v24.3.25/Windows.flatc.binary.zip"

parser = argparse.ArgumentParser()
parser.add_argument("--platform", type=str, default="linux")
args = parser.parse_args()

# Download flatc if not exist
if not os.path.exists(FLATC_PATH):
    import stat
    from io import BytesIO
    from urllib.request import urlopen
    from zipfile import ZipFile

    print(f"{FLATC_PATH} not found. Downloading...")
    
    os.makedirs("bin", exist_ok=True)

    if args.platform == "linux":
        url = FLATC_URL_LINUX
    elif args.platform == "windows":
        url = FLATC_URL_WINDOWS

    resp = urlopen(url)
    file = ZipFile(BytesIO(resp.read()))
    file.extract("flatc", path="./bin")

    # chmod +x
    st = os.stat("bin/flatc")
    os.chmod("bin/flatc", st.st_mode | stat.S_IEXEC)

# Compile schemas
packet_schemas = pathlib.Path("Schemas/packet").rglob("*.fbs")
world_schemas = pathlib.Path("Schemas/world").rglob("*.fbs")

flatc_args = [FLATC_PATH, "--csharp", "--filename-suffix", '',
    "-o", "Network/bin/packet_schemas", "-I", "Schemas/packet"]
flatc_args += [schema.as_posix() for schema in packet_schemas]

print(flatc_args)
subprocess.run(flatc_args)

flatc_args = [FLATC_PATH, "--csharp", "--filename-suffix", '',
    "-o", "Client/bin/world_schemas", "-I", "Schemas/world"]
flatc_args += [schema.as_posix() for schema in world_schemas]

print(flatc_args)
subprocess.run(flatc_args)