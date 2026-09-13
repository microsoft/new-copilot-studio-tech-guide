#!/usr/bin/env python3
"""Validate committed Power Platform solution packages before import."""

import json
from pathlib import Path
import re
import sys
from zipfile import BadZipFile, ZipFile


REPO_ROOT = Path(__file__).resolve().parents[1]
SOLUTION_ROOT = REPO_ROOT / "sample" / "solution"
REQUIRED_ROOT_FILES = {
    "solution.xml",
    "customizations.xml",
    "[Content_Types].xml",
}
AGENT_CONFIGURATION = (
    "bots/crf98_curbsidepickupassistant_Q0mRmz/configuration.json"
)
AGENT_STATE_FILES = (
    "botcomponents/"
    "crf98_curbsidepickupassistant_Q0mRmz.file.skillmd_FPadO/"
    "filedata/SKILL.md",
    "botcomponents/"
    "crf98_curbsidepickupassistant_Q0mRmz.file.blastboxstagingpy_RnlUT/"
    "filedata/blastbox_staging.py",
)
CONNECTOR_PACKAGE_FILES = (
    "Connector/cat_curb-20pickup-20mcp_customcodeblobcontent.csx",
    "Connector/cat_curb-20pickup-20mcp_openapidefinition.json",
)


def fail(message):
    raise ValueError(message)


def read_version(data):
    match = re.search(rb"<Version>([^<]+)</Version>", data)
    if not match:
        fail("solution.xml does not contain a solution version")
    return match.group(1).decode("utf-8")


def validate_package(zip_path, source_path):
    if not zip_path.is_file():
        fail(f"missing solution package: {zip_path}")
    if not source_path.is_dir():
        fail(f"missing unpacked solution source: {source_path}")

    other_path = source_path / "Other"
    expected_other = {"Solution.xml", "Customizations.xml"}
    actual_other = (
        {entry.name for entry in other_path.iterdir() if entry.is_file()}
        if other_path.is_dir()
        else set()
    )
    if actual_other != expected_other:
        fail(
            f"{source_path.name}/Other must contain exactly "
            f"{sorted(expected_other)}; found {sorted(actual_other)}"
        )

    try:
        with ZipFile(zip_path) as archive:
            names = archive.namelist()
            if len(names) != len(set(names)):
                fail(f"{zip_path.name} contains duplicate ZIP entries")

            root_files = {
                name
                for name in names
                if "/" not in name.rstrip("/")
            }
            missing = REQUIRED_ROOT_FILES - root_files
            if missing:
                fail(
                    f"{zip_path.name} is not importable: missing root files "
                    f"{sorted(missing)}. Pack with 'pac solution pack'; do not "
                    "ZIP the unpacked source directory directly."
                )
            if any(name.lower().startswith("other/") for name in names):
                fail(
                    f"{zip_path.name} incorrectly contains an Other/ folder. "
                    "Other/ belongs only in unpacked source."
                )

            source_solution = other_path / "Solution.xml"
            packaged_version = read_version(archive.read("solution.xml"))
            source_version = read_version(source_solution.read_bytes())
            if packaged_version != source_version:
                fail(
                    f"{zip_path.name} version {packaged_version} does not match "
                    f"source version {source_version}"
                )

            if source_path.name == "BlastBoxAgents":
                if AGENT_CONFIGURATION not in names:
                    fail(
                        f"{zip_path.name} is missing {AGENT_CONFIGURATION}"
                    )
                packaged_config = archive.read(AGENT_CONFIGURATION)
                source_config = (source_path / AGENT_CONFIGURATION).read_bytes()
                if packaged_config != source_config:
                    fail(
                        "packaged Curb Pickup Assistant configuration does not "
                        "match unpacked source"
                    )
                config = json.loads(packaged_config)
                segments = config["agentSettings"]["instructions"]["segments"]
                if len(segments) != 1:
                    fail(
                        "Curb Pickup Assistant must contain exactly one "
                        "instruction segment so Copilot Studio renders it"
                    )
                instructions = segments[0].get("value", "").strip()
                if not instructions:
                    fail("Curb Pickup Assistant instructions are empty")
                for required_text in (
                    "SAME-SESSION STATE",
                    "ROUTING PRECEDENCE",
                    "recommendation_evidence",
                    "CUSTOMER OUTPUT",
                    "curbside-bay-selector",
                ):
                    if required_text not in instructions:
                        fail(
                            "Curb Pickup Assistant instructions are missing "
                            f"required text: {required_text}"
                        )
                for package_name in AGENT_STATE_FILES:
                    source_file = source_path / package_name
                    if archive.read(package_name) != source_file.read_bytes():
                        fail(
                            f"packaged agent file does not match source: "
                            f"{package_name}"
                        )

            if source_path.name == "BlastBoxConnectors":
                canonical_root = SOLUTION_ROOT / "connectors" / "curb-delivery-mcp"
                connector_pairs = (
                    (
                        canonical_root / "script.csx",
                        source_path
                        / "Connectors"
                        / "cat_curb-20pickup-20mcp_customcodeblobcontent.csx",
                        CONNECTOR_PACKAGE_FILES[0],
                    ),
                    (
                        canonical_root / "apiDefinition.json",
                        source_path
                        / "Connectors"
                        / "cat_curb-20pickup-20mcp_openapidefinition.json",
                        CONNECTOR_PACKAGE_FILES[1],
                    ),
                )
                for canonical_file, source_file, package_name in connector_pairs:
                    canonical_data = canonical_file.read_bytes()
                    source_data = source_file.read_bytes()
                    if canonical_data != source_data:
                        fail(
                            "canonical connector file does not match unpacked "
                            f"solution source: {canonical_file.name}"
                        )
                    if archive.read(package_name) != source_data:
                        fail(
                            "packaged connector file does not match unpacked "
                            f"solution source: {package_name}"
                        )

            print(
                f"[ok] {zip_path.name}: importable, source-matched, "
                f"version {packaged_version}"
            )
    except BadZipFile as exc:
        fail(f"{zip_path.name} is not a valid ZIP file: {exc}")


def main():
    packages = (
        (
            SOLUTION_ROOT / "BlastBoxConnectors_1_0_0_1.zip",
            SOLUTION_ROOT / "src" / "BlastBoxConnectors",
        ),
        (
            SOLUTION_ROOT / "BlastBoxAgents_1_0_0_1.zip",
            SOLUTION_ROOT / "src" / "BlastBoxAgents",
        ),
    )
    try:
        for zip_path, source_path in packages:
            validate_package(zip_path, source_path)
    except (KeyError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"[error] {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
