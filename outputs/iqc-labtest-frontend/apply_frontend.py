"""Apply only the reviewed tab files, preserving pre-existing frontend changes."""
from pathlib import Path
import hashlib
import json
import subprocess

package = Path(__file__).resolve().parent
root = Path('F:/UserInterface/hrm-mobile').resolve()
baseline = json.loads((package / 'baseline.json').read_text(encoding='utf-8'))
sources = sorted(p for p in (package / 'files').rglob('*') if p.is_file())
hash_of = lambda data: hashlib.sha256(data).hexdigest()
branch = subprocess.check_output(['git', 'branch', '--show-current'], cwd=root, text=True).strip()
if branch != 'codex/feat-employee-administration-lifecycle':
    raise RuntimeError('Frontend branch changed; do not write')
for source in sources:
    relative = source.relative_to(package / 'files')
    target = (root / relative).resolve()
    if root not in target.parents:
        raise RuntimeError('Target outside frontend root')
    expected = baseline.get(relative.as_posix())
    if target.exists() and target.read_bytes() == source.read_bytes():
        continue
    if expected is None and target.exists():
        raise RuntimeError(f'New target already exists: {relative}')
    if expected is not None and hash_of(target.read_bytes()) != expected:
        raise RuntimeError(f'Existing file changed: {relative}')
for source in sources:
    target = root / source.relative_to(package / 'files')
    if target.exists() and target.read_bytes() == source.read_bytes():
        continue
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(source.read_bytes())
applied = {source.relative_to(package / 'files').as_posix(): hash_of(source.read_bytes()) for source in sources}
assert all(hash_of((root / name).read_bytes()) == value for name, value in applied.items())
(package / 'applied.json').write_text(json.dumps(applied, indent=2), encoding='utf-8')
print(f'Applied {len(sources)} reviewed frontend files; branch unchanged.')
