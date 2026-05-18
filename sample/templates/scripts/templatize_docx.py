"""
Convert a Word document into a template by replacing every shape's and
paragraph's text with a {{name}} placeholder. Preserves run formatting,
table structure, text boxes, and content controls (SDTs).

Walks:
  - Body paragraphs and tables
  - Text boxes inside drawings (w:txbxContent)
  - Structured Document Tags / content controls (w:sdt)
  - Headers and footers

Usage:
    python templatize_docx.py input.docx output.docx
"""

import re
import sys

from docx import Document
from docx.oxml.ns import qn

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"


def slugify(name: str) -> str:
    name = re.sub(r"[^A-Za-z0-9]+", "_", name or "field").strip("_").lower()
    return name or "field"


class PlaceholderNamer:
    def __init__(self):
        self.seen: dict[str, int] = {}

    def next(self, raw_name: str) -> str:
        base = slugify(raw_name)
        count = self.seen.get(base, 0) + 1
        self.seen[base] = count
        return base if count == 1 else f"{base}_{count}"


def paragraph_text(p_elem) -> str:
    parts = []
    for t in p_elem.iter(qn("w:t")):
        parts.append(t.text or "")
    return "".join(parts)


def replace_paragraph_text(p_elem, token: str) -> None:
    """Replace paragraph text with {{token}}, keeping the first run's formatting."""
    runs = p_elem.findall(qn("w:r"))
    if not runs:
        # No runs -- create a minimal run.
        r = p_elem.makeelement(qn("w:r"), {})
        t = p_elem.makeelement(qn("w:t"), {})
        t.text = f"{{{{{token}}}}}"
        r.append(t)
        p_elem.append(r)
        return

    first_run = runs[0]

    # Remove all but the first run.
    for r in runs[1:]:
        p_elem.remove(r)

    # Inside the first run, drop everything except w:rPr (formatting), then add a fresh w:t.
    rPr = first_run.find(qn("w:rPr"))
    for child in list(first_run):
        if child.tag != qn("w:rPr"):
            first_run.remove(child)
    if rPr is None:
        # rPr was absent -- nothing extra to do.
        pass

    t = first_run.makeelement(qn("w:t"), {"{http://www.w3.org/XML/1998/namespace}space": "preserve"})
    t.text = f"{{{{{token}}}}}"
    first_run.append(t)


def container_name(elem) -> str:
    """Best-effort name for a container (text box, sdt, etc.)."""
    # Text boxes: the parent drawing has a wp:docPr / wps:cNvPr with a name attr.
    parent = elem.getparent()
    while parent is not None:
        for child in parent.iter():
            tag = child.tag.split("}")[-1]
            if tag in ("docPr", "cNvPr") and child.get("name"):
                return child.get("name")
        parent = parent.getparent() if hasattr(parent, "getparent") else None
        # Don't walk forever.
        break

    # SDT alias / tag.
    sdtPr = elem.find(qn("w:sdtPr"))
    if sdtPr is not None:
        alias = sdtPr.find(qn("w:alias"))
        if alias is not None and alias.get(qn("w:val")):
            return alias.get(qn("w:val"))
        tag = sdtPr.find(qn("w:tag"))
        if tag is not None and tag.get(qn("w:val")):
            return tag.get(qn("w:val"))
    return ""


def process_paragraphs_in(scope_elem, namer: PlaceholderNamer, default_prefix: str = "field") -> int:
    """Walk every w:p inside scope_elem and replace its text if non-empty."""
    count = 0
    for p in scope_elem.iter(qn("w:p")):
        text = paragraph_text(p).strip()
        if not text:
            continue

        # Try to derive a name from the nearest ancestor container.
        name = ""
        ancestor = p.getparent()
        while ancestor is not None:
            tag = ancestor.tag.split("}")[-1]
            if tag in ("txbxContent", "sdtContent", "tc"):
                name = container_name(ancestor.getparent() if tag == "sdtContent" else ancestor)
                if name:
                    break
            ancestor = ancestor.getparent() if hasattr(ancestor, "getparent") else None

        token = namer.next(name or default_prefix)
        replace_paragraph_text(p, token)
        count += 1
    return count


def templatize(src: str, dst: str) -> None:
    doc = Document(src)
    namer = PlaceholderNamer()
    total = 0

    # Body
    total += process_paragraphs_in(doc.element.body, namer, default_prefix="body")

    # Headers and footers
    for section in doc.sections:
        for hf in (section.header, section.footer,
                   section.first_page_header, section.first_page_footer,
                   section.even_page_header, section.even_page_footer):
            try:
                total += process_paragraphs_in(hf._element, namer, default_prefix="header_footer")
            except Exception:
                continue

    doc.save(dst)
    print(f"Done. {total} placeholder(s) written to {dst}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python templatize_docx.py input.docx output.docx")
        sys.exit(1)
    templatize(sys.argv[1], sys.argv[2])
