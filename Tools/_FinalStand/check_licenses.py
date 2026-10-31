#!/usr/bin/env python3
import json
import re
import subprocess
import sys

FORK_FOLDER = re.compile(r"(^|/)_(Goobstation|Shitmed|Shitcode|EinsteinEngines|EE|DV|DeltaV|NF|White|WWDP|Monolith|Mono|Impstation|Floof|Starlight|Lavaland)(/|$)")
FORK_PROJECT = re.compile(r"^Content\.(Goobstation|Shitmed|ModuleManager)")
COPYLEFT_TEXT = re.compile(r"SPDX-License-Identifier:\s*(AGPL|GPL|LGPL|MPL)|GNU Affero General Public License|AGPL-3\.0")
FORK_CODE = re.compile(r"\bContent\.(Goobstation|Shitmed)\b|\bnamespace\s+[\w.]*\._(Goobstation|Shitmed|Shitcode|EinsteinEngines|DV|NF|Monolith)\b")
COPYLEFT_ASSET = re.compile(r"^(AGPL|GPL|LGPL|MPL)", re.I)
TEXT_EXT = (".cs", ".xaml", ".yml", ".yaml", ".ftl", ".json", ".csproj", ".slnx", ".props", ".targets", ".toml", ".py", ".ps1", ".sh", ".md", ".txt", ".xml")
SKIP = ("Tools/_FinalStand/check_licenses.py",)


def tracked_files():
    out = subprocess.run(["git", "ls-files", "-z"], capture_output=True, check=True).stdout
    return [p for p in out.decode("utf-8").split("\0") if p]


def main():
    problems = []
    for path in tracked_files():
        if path in SKIP:
            continue

        if FORK_FOLDER.search(path) or FORK_PROJECT.match(path):
            problems.append(f"{path}: fork code folder")
            continue

        if not path.endswith(TEXT_EXT):
            continue

        try:
            text = open(path, encoding="utf-8", errors="replace").read()
        except OSError:
            continue

        if COPYLEFT_TEXT.search(text):
            problems.append(f"{path}: copyleft licence header")

        if path.endswith((".cs", ".xaml", ".csproj", ".slnx")) and FORK_CODE.search(text):
            problems.append(f"{path}: reference to fork code")

        if path.endswith(".rsi/meta.json"):
            try:
                licence = json.loads(text).get("license", "")
            except ValueError:
                licence = ""
            if COPYLEFT_ASSET.match(licence):
                problems.append(f"{path}: copyleft asset licence {licence}")

    for problem in problems:
        print(problem)

    if problems:
        print(f"\n{len(problems)} licence problem(s) found.")
        return 1

    print("No copyleft or fork code found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
