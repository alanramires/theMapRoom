#!/usr/bin/env python3
"""Audit panel catalogs without Unity or third-party dependencies.

Usage: python tools/audit_panel_messages.py [--write-index]
Checks IDs, GUID registration, literal message references and named arguments in
PanelMessage calls. Dynamic IDs and externally supplied content need manual review.
"""
from pathlib import Path
import argparse
import ast
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / 'Assets/DB/Messages'
ID = re.compile(r'^  id: (.+)$', re.M)
KEY = re.compile(r'"([a-z][a-z0-9_]*(?:\.[A-Za-z0-9_]+)+)"')
TOKEN = re.compile(r'<([A-Za-z_][A-Za-z0-9_]*)>')
TAGS = {'b', 'i', 's', 'u', 'color', 'size', 'br', 'sub', 'sup', 'uppercase', 'lowercase', 'smallcaps', 'nobr'}


def scalar(text, field):
    match = re.search(r'^  ' + field + r':(.*)((?:\n(?:    .*|[ \t]*))*)', text, re.M)
    if not match:
        return ''
    value = ' '.join(x.strip() for x in (match.group(1) + match.group(2)).splitlines()).strip()
    if value.startswith('"') and value.endswith('"'):
        try:
            return ast.literal_eval(value)
        except (SyntaxError, ValueError):
            return value
    if value.startswith("'") and value.endswith("'"):
        return value[1:-1].replace("''", "'")
    return value


def call_end(text, start):
    depth = 1
    quote = False
    escaped = False
    for i in range(start, len(text)):
        c = text[i]
        if quote:
            if escaped:
                escaped = False
            elif c == '\\':
                escaped = True
            elif c == '"':
                quote = False
        elif c == '"':
            quote = True
        elif c == '(':
            depth += 1
        elif c == ')':
            depth -= 1
            if depth == 0:
                return i
    raise ValueError('Unterminated PanelMessage call')


def audit(write_index=False):
    errors, entries, references, translations = [], {}, {}, {}
    databases = {kind: (CATALOG / (kind + ' Database.asset')).read_text(encoding='utf-8-sig') for kind in ('Helper', 'Dialog')}
    catalog_guids = {}
    for path in sorted(CATALOG.rglob('*.asset')):
        text = path.read_text(encoding='utf-8-sig')
        match = ID.search(text)
        if not match:
            continue
        key = match.group(1).strip()
        if key in entries:
            errors.append('Duplicate ID: ' + key)
        message = scalar(text, 'message')
        entries[key] = (path, message)
        english = scalar(text, 'messageEnglish')
        translations[key] = english
        if english.strip():
            contract = lambda value: {t.lower() for t in re.findall(r'<([A-Za-z_][A-Za-z0-9_. ]*)>', value)} - TAGS
            if contract(message) != contract(english):
                errors.append('English token mismatch: ' + key)
        if not message.strip():
            errors.append('Empty message: ' + key)
        meta = Path(str(path) + '.meta')
        guid_match = re.search(r'^guid: (.+)$', meta.read_text(), re.M) if meta.exists() else None
        if not guid_match:
            errors.append('Missing meta/GUID: ' + str(path))
            continue
        guid = guid_match.group(1)
        catalog_guids[guid] = key
        if 'UI Text Data' in path.parts:
            continue  # Fixed TMP labels reference assets directly, without a database.
        kind = 'Helper' if 'Helper Data' in path.parts else 'Dialog'
        if databases[kind].count('guid: ' + guid + ',') != 1:
            errors.append('Must be registered exactly once in ' + kind + ' Database: ' + key)
    for kind, text in databases.items():
        section = text.split('  messages:', 1)[1]
        for guid in re.findall(r'guid: ([a-f0-9]+)', section):
            if guid not in catalog_guids:
                errors.append(kind + ' Database has an unknown GUID: ' + guid)
    prefixes = {key.split('.')[0] for key in entries}
    for path in sorted((ROOT / 'Assets/Scripts').rglob('*.cs')):
        if any(part.endswith('~') for part in path.parts):
            continue
        text = path.read_text(encoding='utf-8-sig', errors='replace')
        relative = path.relative_to(ROOT).as_posix()
        if re.search(r'\[PanelMessage\.(?:Helper|Dialog)\(', text):
            errors.append(relative + ': translated dictionary key; token identifiers must stay stable')
        for number, line in enumerate(text.splitlines(), 1):
            if line.lstrip().startswith('//') or re.match(r'\s*\{\s*"', line):
                continue
            for match in KEY.finditer(line):
                key = match.group(1)
                if key.endswith('.') or key.split('.')[0] not in prefixes:
                    continue
                references.setdefault(key, []).append((relative, number))
                if key not in entries:
                    errors.append(f'{relative}:{number}: missing asset {key}')
        for match in re.finditer(r'PanelMessage\.(Helper|Dialog)\(\s*"([^"]+)"', text):
            key = match.group(2)
            if key not in entries:
                continue
            start = text.index('(', match.start()) + 1
            body = text[start:call_end(text, start)]
            supplied = set(re.findall(r'\(\s*"([A-Za-z_][A-Za-z0-9_]*)"\s*,', body))
            required = set(TOKEN.findall(entries[key][1])) - TAGS
            missing = {x for x in required if x.lower() not in {y.lower() for y in supplied}}
            if missing:
                errors.append(f'{relative}:{text.count(chr(10), 0, match.start())+1}: {key} missing arguments {sorted(missing)}')
    if write_index:
        output = ROOT / 'docs/i18n/panel_messages_index.md'
        output.parent.mkdir(parents=True, exist_ok=True)
        lines = ['# Índice de mensagens dos painéis', '', 'Gerado por `python tools/audit_panel_messages.py --write-index`.', '',
                 'As referências são literais; chaves dinâmicas e conteúdo recebido de outras fichas exigem rastreamento manual. Edite os assets, não este índice.', '']
        for key, (path, message) in sorted(entries.items()):
            relative = path.relative_to(ROOT).as_posix()
            lines += ['## ' + key, '', f'Asset: [{path.name}](../../{relative.replace(" ", "%20")})', '',
                      '```text', message, '```', '', 'Referências:']
            lines += [''] + ([f'- `{p}:{n}`' for p, n in references.get(key, [])] or ['- Sem referência literal encontrada.']) + ['']
            if translations[key].strip():
                lines += ['English:', '', '```text', translations[key], '```', '']
        output.write_text('\n'.join(line.rstrip() for block in lines for line in block.split('\n')), encoding='utf-8')
    errors = sorted(set(errors))
    print(f'{len(entries)} messages; {len(references)} referenced IDs; {len(errors)} errors.')
    for error in errors:
        print(error)
    return 1 if errors else 0


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write-index', action='store_true')
    sys.exit(audit(parser.parse_args().write_index))
