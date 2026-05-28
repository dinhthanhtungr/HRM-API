from pathlib import Path
import re

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


SOURCE = Path("workdocs/Phong hop VietAus raw.docx")
OUTPUT = Path("workdocs/Phong hop VietAus - chuan hoa.docx")


def fix_mojibake(text: str) -> str:
    try:
        fixed = text.encode("latin1").decode("utf-8")
        if fixed.count("�") <= text.count("�"):
            return fixed
    except (UnicodeEncodeError, UnicodeDecodeError):
        pass
    return text


def cleanup_line(text: str) -> str:
    text = fix_mojibake(text)
    text = text.replace("\xa0", " ")
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r"\s+([,.?!:;])", r"\1", text)
    text = re.sub(r"([,.?!:;])(?=[^\s,.?!:;])", r"\1 ", text)
    text = re.sub(r"\.{3,}", "...", text)
    text = re.sub(r"\.{2}$", ".", text)
    text = re.sub(r"\?+\.", "?", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text


def split_speaker_block(text: str):
    text = fix_mojibake(text)
    parts = [cleanup_line(part) for part in text.splitlines()]
    return [part for part in parts if part]


def add_paragraph(doc: Document, text: str, style: str | None = None):
    paragraph = doc.add_paragraph(style=style)
    paragraph.paragraph_format.space_after = Pt(4)
    paragraph.paragraph_format.line_spacing = 1.08
    paragraph.add_run(text)
    return paragraph


raw_doc = Document(SOURCE)
lines: list[str] = []
for paragraph in raw_doc.paragraphs:
    lines.extend(split_speaker_block(paragraph.text))

for table_index, table in enumerate(raw_doc.tables, start=1):
    lines.append(f"[Bảng {table_index}]")
    for row in table.rows:
        cells = [cleanup_line(cell.text.replace("\n", " ")) for cell in row.cells]
        if any(cells):
            lines.append(" | ".join(cells))

doc = Document()
section = doc.sections[0]
section.top_margin = Inches(0.75)
section.bottom_margin = Inches(0.75)
section.left_margin = Inches(0.8)
section.right_margin = Inches(0.8)

styles = doc.styles
styles["Normal"].font.name = "Arial"
styles["Normal"]._element.rPr.rFonts.set(qn("w:eastAsia"), "Arial")
styles["Normal"].font.size = Pt(10.5)

title = doc.add_paragraph()
title.alignment = WD_ALIGN_PARAGRAPH.CENTER
title_run = title.add_run("PHÒNG HỌP VIETAUS - BẢN CHUẨN HÓA TRANSCRIPT")
title_run.bold = True
title_run.font.size = Pt(15)
title_run.font.color.rgb = RGBColor(31, 78, 121)

subtitle = doc.add_paragraph()
subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER
subtitle.paragraph_format.space_after = Pt(12)
subtitle_run = subtitle.add_run("Chuẩn hóa từ dữ liệu speech-to-text, giữ đầy đủ nội dung theo thứ tự bản gốc")
subtitle_run.italic = True
subtitle_run.font.size = Pt(9.5)
subtitle_run.font.color.rgb = RGBColor(89, 89, 89)

metadata = []
body_start_index = 0
for index, line in enumerate(lines[:4]):
    metadata.append(line)
    body_start_index = index + 1

labels = ["Tên bản ghi", "Thời gian", "Thời lượng", "Trạng thái"]
for label, value in zip(labels, metadata):
    paragraph = doc.add_paragraph()
    paragraph.paragraph_format.left_indent = Inches(0.25)
    paragraph.paragraph_format.space_after = Pt(2)
    label_run = paragraph.add_run(f"{label}: ")
    label_run.bold = True
    label_run.font.color.rgb = RGBColor(31, 78, 121)
    paragraph.add_run(value)

doc.add_paragraph()

heading = doc.add_paragraph()
heading_run = heading.add_run("Nội dung transcript")
heading_run.bold = True
heading_run.font.size = Pt(12.5)
heading_run.font.color.rgb = RGBColor(31, 78, 121)
heading.paragraph_format.space_before = Pt(6)
heading.paragraph_format.space_after = Pt(6)

for line in lines[body_start_index:]:
    if not line:
        continue

    match = re.match(r"^(.*?)(\s+\d{1,2}:\d{2})$", line)
    if line.lower().endswith("started transcription") or line.lower().endswith("stopped transcription"):
        paragraph = add_paragraph(doc, line)
        paragraph.runs[0].italic = True
        paragraph.runs[0].font.color.rgb = RGBColor(89, 89, 89)
    elif match:
        paragraph = add_paragraph(doc, line)
        paragraph.runs[0].bold = True
        paragraph.runs[0].font.color.rgb = RGBColor(31, 78, 121)
    else:
        add_paragraph(doc, line)

doc.save(OUTPUT)
print(OUTPUT)
