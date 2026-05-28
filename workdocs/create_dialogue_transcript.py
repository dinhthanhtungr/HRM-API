from pathlib import Path
import re

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


SOURCE = Path("workdocs/Phong hop VietAus raw.docx")
OUTPUT = Path("workdocs/Phong hop VietAus - bien tap hoi thoai.docx")


def fix_mojibake(text: str) -> str:
    try:
        fixed = text.encode("latin1").decode("utf-8")
        if fixed.count("�") <= text.count("�"):
            return fixed
    except (UnicodeEncodeError, UnicodeDecodeError):
        pass
    return text


def clean_text(text: str) -> str:
    text = fix_mojibake(text).replace("\xa0", " ")
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r"\s+([,.?!:;])", r"\1", text)
    text = re.sub(r"([,.?!:;])(?=[^\s,.?!:;])", r"\1 ", text)
    text = re.sub(r"\.{3,}", "...", text)
    text = re.sub(r"\.{2,}$", ".", text)
    text = re.sub(r"\?+\.", "?", text)
    text = re.sub(r"\s+", " ", text).strip()

    replacements = {
        "ok": "OK",
        "Ok": "OK",
        "kg": "kg",
        "Kg": "kg",
        "chủ nhật": "Chủ nhật",
        "vĩnh lộc": "Vĩnh Lộc",
        "huỳnh vinh": "Huỳnh Vinh",
        "vina star": "Vina Star",
        "vinasstar": "Vina Star",
        "bb": "BB",
        "bbc": "BBC",
        "bdc": "BDC",
        "tdf": "TDF",
        "hpo": "HPO",
        "npt": "NPT",
        "ftm": "FTM",
    }
    for old, new in replacements.items():
        text = re.sub(rf"\b{re.escape(old)}\b", new, text, flags=re.IGNORECASE)

    text = re.sub(r"\b(\d+)\s+\.\s*(\d{3})\b", r"\1.\2", text)
    text = re.sub(r"\b(\d+)\s+(\d+)\s*(kg|ký)\b", r"\1-\2 \3", text, flags=re.IGNORECASE)
    return text


def extract_lines(path: Path) -> list[str]:
    doc = Document(path)
    lines: list[str] = []
    for paragraph in doc.paragraphs:
        for part in fix_mojibake(paragraph.text).splitlines():
            cleaned = clean_text(part)
            if cleaned:
                lines.append(cleaned)
    for table_index, table in enumerate(doc.tables, start=1):
        lines.append(f"[Bảng {table_index}]")
        for row in table.rows:
            cells = [clean_text(cell.text.replace("\n", " ")) for cell in row.cells]
            if any(cells):
                lines.append(" | ".join(cells))
    return lines


def is_noise_break(line: str) -> bool:
    return line in {".", "..", "..."}


def normalize_sentence(line: str) -> str:
    if is_noise_break(line):
        return "[ngắt đoạn/âm thanh không rõ]"

    unclear_short = {
        "Là.",
        "Giờ.",
        "Trước.",
        "Và.",
        "Thì.",
        "Dạ.",
        "Em.",
        "Gì?",
        "Chắc.",
        "Nhạc.",
        "Thôi.",
    }
    if line in unclear_short:
        return f"{line} [câu ngắn, cần đối chiếu âm thanh nếu dùng chính thức]"

    if not re.search(r"[.!?]$", line):
        line += "."
    return line


def should_start_new_turn(current: list[str], next_line: str) -> bool:
    if not current:
        return False
    if len(current) >= 6:
        return True
    if is_noise_break(next_line):
        return True
    if next_line.endswith("?") and len(current) >= 2:
        return True
    topic_starters = (
        "Bên ",
        "Còn ",
        "Với lại",
        "Vấn đề",
        "Bây giờ",
        "Hôm bữa",
        "Khách",
        "Thì lúc",
        "Đúng rồi",
    )
    return len(current) >= 3 and next_line.startswith(topic_starters)


def build_turns(lines: list[str]) -> tuple[list[str], list[list[str]]]:
    metadata = lines[:4]
    body = lines[4:]
    turns: list[list[str]] = []
    current: list[str] = []

    for line in body:
        if line.lower().endswith("started transcription"):
            metadata[-1] = line
            continue
        if line.lower().endswith("stopped transcription"):
            if current:
                turns.append(current)
                current = []
            turns.append([line])
            continue
        if re.match(r"^Admin VietAus\s+\d{1,2}:\s*\d{2}$", line):
            if current:
                turns.append(current)
            current = [line]
            continue
        if should_start_new_turn(current, line):
            turns.append(current)
            current = []
        current.append(normalize_sentence(line))

    if current:
        turns.append(current)

    return metadata, turns


def add_run(paragraph, text, bold=False, italic=False, color=None, size=None):
    run = paragraph.add_run(text)
    run.bold = bold
    run.italic = italic
    if color:
        run.font.color.rgb = RGBColor(*color)
    if size:
        run.font.size = Pt(size)
    return run


lines = extract_lines(SOURCE)
metadata, turns = build_turns(lines)

doc = Document()
section = doc.sections[0]
section.top_margin = Inches(0.7)
section.bottom_margin = Inches(0.7)
section.left_margin = Inches(0.8)
section.right_margin = Inches(0.8)

styles = doc.styles
styles["Normal"].font.name = "Arial"
styles["Normal"]._element.rPr.rFonts.set(qn("w:eastAsia"), "Arial")
styles["Normal"].font.size = Pt(10.5)

title = doc.add_paragraph()
title.alignment = WD_ALIGN_PARAGRAPH.CENTER
add_run(title, "PHÒNG HỌP VIETAUS - BẢN BIÊN TẬP HỘI THOẠI", True, color=(31, 78, 121), size=15)

subtitle = doc.add_paragraph()
subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER
subtitle.paragraph_format.space_after = Pt(10)
add_run(
    subtitle,
    "Biên tập từ speech-to-text: gom câu vụn thành lượt trao đổi, giữ đầy đủ nội dung theo thứ tự gốc",
    italic=True,
    color=(89, 89, 89),
    size=9.5,
)

labels = ["Tên bản ghi", "Thời gian", "Thời lượng", "Trạng thái"]
for label, value in zip(labels, metadata):
    paragraph = doc.add_paragraph()
    paragraph.paragraph_format.left_indent = Inches(0.2)
    paragraph.paragraph_format.space_after = Pt(1)
    add_run(paragraph, f"{label}: ", True, color=(31, 78, 121))
    add_run(paragraph, value)

note = doc.add_paragraph()
note.paragraph_format.space_before = Pt(6)
note.paragraph_format.space_after = Pt(10)
add_run(note, "Ghi chú biên tập: ", True, color=(192, 0, 0))
add_run(
    note,
    "Những câu quá ngắn hoặc không đủ ngữ cảnh được giữ lại và đánh dấu, thay vì tự ý bỏ hoặc đoán sai nội dung.",
    italic=True,
    color=(89, 89, 89),
)

heading = doc.add_paragraph()
heading.paragraph_format.space_before = Pt(4)
heading.paragraph_format.space_after = Pt(6)
add_run(heading, "Nội dung hội thoại đã biên tập", True, color=(31, 78, 121), size=12.5)

turn_number = 1
for turn in turns:
    if not turn:
        continue
    if len(turn) == 1 and turn[0].lower().endswith("stopped transcription"):
        paragraph = doc.add_paragraph()
        paragraph.paragraph_format.space_before = Pt(8)
        add_run(paragraph, turn[0], italic=True, color=(89, 89, 89))
        continue

    paragraph = doc.add_paragraph()
    paragraph.paragraph_format.space_before = Pt(5)
    paragraph.paragraph_format.space_after = Pt(2)
    paragraph.paragraph_format.line_spacing = 1.08

    if re.match(r"^Admin VietAus", turn[0]):
        add_run(paragraph, f"Lượt {turn_number:02d} - {turn[0]}: ", True, color=(31, 78, 121))
        content = " ".join(turn[1:]).strip()
    else:
        add_run(paragraph, f"Lượt {turn_number:02d}: ", True, color=(31, 78, 121))
        content = " ".join(turn).strip()

    add_run(paragraph, content)
    turn_number += 1

doc.save(OUTPUT)
print(OUTPUT)
