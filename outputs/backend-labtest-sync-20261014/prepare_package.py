"""Prepare a reviewable model package; Backend is strictly read-only."""
from pathlib import Path
import difflib
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = Path(__file__).resolve().parent
BACKEND = Path('F:/Backend')

def convert(text):
    return text.replace('HRM.Domain', 'VietausWebAPI.Core.Domain').replace('HRM.Infrastructure', 'VietausWebAPI.Infrastructure')

def digest(data):
    return hashlib.sha256(data).hexdigest()

def properties(text):
    return {
        name: kind for kind, name in re.findall(r'public\s+(?:virtual\s+)?([\w?.<>]+)\s+(\w+)\s*\{\s*get;\s*set;', text)
    }

def normalized(text):
    text = re.sub(r'///[^\n]*', '', text)
    text = re.sub(r'^using .*?;\s*', '', text, flags=re.M)
    text = text.replace('VietausWebAPI.Core.Domain.Enums.Devandqa.EvaluationDataType.', 'EvaluationDataType.')
    return re.sub(r'\s+', '', text)

names = ['EvaluationStandard', 'MeasurementMethod', 'EvaluationTemplate', 'EvaluationTemplateItem',
         'IqcSpecification', 'IqcSpecificationItem', 'IqcInspectionEvaluation', 'IqcInspectionResult',
         'LabTest', 'LabTestResult']
pairs = []
for name in names:
    pairs.append((f'HRM.Domain/Entities/DevandqaSchema/{name}.cs', f'VietausWebAPI.Core/Domain/Entities/DevandqaSchema/{name}.cs', 'entity'))
    pairs.append((f'HRM.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/{name}Configuration.cs', f'VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/{name}Configuration.cs', 'configuration'))
pairs.append(('HRM.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/EvaluationSnapshotConfiguration.cs', 'VietausWebAPI.Infrastructure/DatabaseContext/Configurations/DevandqaSchema/EvaluationSnapshotConfiguration.cs', 'configuration'))
for name in ['EvaluationDataType', 'EvaluationTemplateType', 'EvaluationTemplateStatus', 'IqcSpecificationStatus']:
    pairs.append((f'HRM.Domain/Enums/Devandqa/{name}.cs', f'VietausWebAPI.Core/Domain/Enums/Devandqa/{name}.cs', 'enum'))
pairs.append(('HRM.Infrastructure/DatabaseContext/ApplicationDbs/ApplicationDbContext.LabTests.cs', 'VietausWebAPI.Infrastructure/DatabaseContext/ApplicationDbs/ApplicationDbContext.LabTests.cs', 'dbset'))

manifest, patches, rows = [], [], []
for source, target, kind in pairs:
    src = ROOT / source
    ref = BACKEND / target
    source_bytes = src.read_bytes()
    converted = convert(source_bytes.decode('utf-8-sig')).replace('\r\n', '\n')
    old_bytes = ref.read_bytes() if ref.exists() else None
    old = old_bytes.decode('utf-8-sig').replace('\r\n', '\n') if old_bytes is not None else ''
    equivalent = False
    if old_bytes is not None:
        if kind == 'entity':
            equivalent = properties(converted) == properties(old)
        else:
            equivalent = normalized(converted) == normalized(old)
        # IQC result configuration already maps the same collection FK and columns,
        # with the attachment mapping at the end instead of the beginning.
        if target.endswith('/IqcInspectionResultConfiguration.cs'):
            equivalent = sorted(re.findall(r'entity\..*?;', normalized(converted))) == sorted(re.findall(r'entity\..*?;', normalized(old)))
    final = old if equivalent else converted
    if target.endswith('/EvaluationDataType.cs') and old_bytes is not None and not equivalent:
        final, replacements = re.subn(r'MultiSelect\s*=\s*7\s*\n', 'MultiSelect = 7,\n    Attachment = 8\n', old)
        if replacements != 1:
            raise ValueError('EvaluationDataType baseline changed; review before packaging')
    out = PACKAGE / 'files' / target
    out.parent.mkdir(parents=True, exist_ok=True)
    final_bytes = old_bytes if equivalent else final.encode('utf-8')
    out.write_bytes(final_bytes)
    status = 'reference' if equivalent else ('add' if old_bytes is None else 'update')
    if old_bytes is not None:
        baseline = PACKAGE / 'check-baseline' / target
        baseline.parent.mkdir(parents=True, exist_ok=True)
        baseline.write_bytes(old_bytes)
    if status != 'reference':
        patches.append(f'diff --git a/{target} b/{target}\n' + ('new file mode 100644\n' if old_bytes is None else '') + ''.join(difflib.unified_diff(
            old.splitlines(keepends=True), final.splitlines(keepends=True),
            fromfile=f'a/{target}' if old_bytes is not None else '/dev/null', tofile=f'b/{target}')))
    manifest.append({'source': source, 'target': target, 'status': status,
                     'sourceSha256': digest(source_bytes), 'backendSha256': digest(old_bytes) if old_bytes is not None else None,
                     'packageSha256': digest(final_bytes)})
    rows.append(f'| `{target}` | {status} |')

(PACKAGE / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(PACKAGE / 'backend-labtest-sync.patch').write_bytes(''.join(patches).encode('utf-8'))
(PACKAGE / 'COMPARISON.md').write_text('''# Đối chiếu mô hình HRM.api → Backend

Đối chiếu source ngày 2026-10-14; chưa kiểm tra schema DB.
`reference`: Backend đã tương đương về property hoặc mapping, giữ nguyên file Backend.
`add`/`update`: file thực sự nằm trong patch. Không copy cả cây reference đè lên code mới hơn.

| File đích | Trạng thái |
| --- | --- |
''' + '\n'.join(rows) + '''

## Ý nghĩa thay đổi

- LabTest là phiếu test độc lập với tên mẫu/ngày test, company và audit; không cần voucher/QC hoặc MaterialId.
- LabTestResult dùng chung EvaluationStandard/MeasurementMethod/template IQC và lưu snapshot riêng: mã, tên Việt/Anh, datatype, unit, phương pháp, cấu hình, yêu cầu, value và collection file.
- EvaluationDataType bổ sung Attachment=8, giữ nguyên giá trị 1..7.
- Không thêm loại template LabTest: service tương lai chọn template IQC đã Published/có hiệu lực, scope cùng company.
- Danh mục, template, bộ tiêu chuẩn IQC và kết quả IQC hiện có đã tương đương về các property; không tạo danh mục LabTest riêng.
- Backend ApplyConfigurationsFromAssembly đã có; DbSet LabTests ở partial ApplicationDbContext mới.
- Không có API/service/sinh mã/permission/file-save cho LabTest trong gói này.
''', encoding='utf-8')
print(json.dumps({'files': len(manifest), 'changes': [{ 'target': x['target'], 'status': x['status']} for x in manifest if x['status'] != 'reference']}, ensure_ascii=False, indent=2))
