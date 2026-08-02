"""
为 4.0 译文加换行空格后写入 DAT 文件。
流程: 读 XML → 加换行空格 → 读 DAT 保留顺序 → 替换 → 写 DAT

适用: 引擎在第 79 个字符处强制换行的游戏（中文译文需提前切割并填充空格，
借引擎的强制换行实现精准断行）。换行参数需按目标游戏引擎自行调整。
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from datlib import read_dat, write_dat
import xml.etree.ElementTree as ET
import re

# === 路径配置（按实际游戏路径修改） ===
XML_PATH = r"e:\translate\translation\4.0\Data\Text\xml\TranslationManifest.xml"
DAT_PATH = r"e:\translate\translation\4.0\Data\Text\mastertextfile_english.dat"

# === 换行参数 ===
ENGINE_BREAK_POSITION = 79  # 引擎在第79个字符处强制换行
TARGET_BREAK_POSITION = 34  # 在第34个字符处切割文本
SPACE_COUNT = ENGINE_BREAK_POSITION - TARGET_BREAK_POSITION  # 需要填充的空格数
SPACE_STR = ' ' * SPACE_COUNT

# 需要处理的 Key 关键词
INCLUDE_KEYWORDS = ['TOOLTIP', 'ENCYCLOPEDIA', 'PLANET', 'TACTICAL', 'COMMANDER', 'DESCRIPTION', 'FEATURES', 'HERO_SHIP', 'BUILDING']

# 不处理的 Key 关键词
EXCLUDE_KEYWORDS = ['TEXT_INTRO_NOTIFICATION', 'TEXT_SPEECH']

# === 字符判断 ===
def is_cjk(c):
    cp = ord(c)
    return (0x4E00 <= cp <= 0x9FFF or
            0x3400 <= cp <= 0x4DBF or
            0x3000 <= cp <= 0x303F or
            0xFF00 <= cp <= 0xFFEF or
            0xF900 <= cp <= 0xFAFF)

def add_line_breaks(text):
    """为中文文本插入空格断行，在第34字符处硬切割，空格填充到79字符位置"""
    if not text:
        return text
    cjk_count = sum(1 for c in text if is_cjk(c))
    if cjk_count < 15:
        return text
    if re.search(r' {6,}', text):
        return text

    text_len = len(text)
    if text_len <= TARGET_BREAK_POSITION:
        return text

    segments = []
    line_start = 0

    while line_start < text_len:
        remaining = text_len - line_start
        
        if remaining <= TARGET_BREAK_POSITION:
            segments.append(text[line_start:text_len])
            break
        
        # 直接在第 34 字符处硬切割（不再向后检查标点）
        segments.append(text[line_start:line_start + TARGET_BREAK_POSITION])
        segments.append(SPACE_STR)
        line_start += TARGET_BREAK_POSITION

    return ''.join(segments)

def should_process(key):
    key_upper = key.upper()
    for ex in EXCLUDE_KEYWORDS:
        if ex.upper() in key_upper:
            return False
    for inc in INCLUDE_KEYWORDS:
        if inc in key_upper:
            return True
    return False

# === 主流程 ===
def main():
    # 1. 加载 XML 翻译
    print(f"加载 XML: {XML_PATH}")
    translations = {}
    tree = ET.parse(XML_PATH)
    root = tree.getroot()
    for loc in root.findall('.//Localisation'):
        key = loc.get('Key', '')
        for trans in loc.findall('.//Translation'):
            lang = trans.get('Language', '')
            if lang == 'ENGLISH':
                translations[key] = (trans.text or '').replace('\r\n', '\n').replace('\r', '\n')

    print(f"XML 翻译条目: {len(translations)}")

    # 2. 对中文翻译加换行空格
    break_applied = 0
    for key, value in translations.items():
        # 只处理含中文的条目
        if not any(is_cjk(c) for c in value):
            continue
        if not should_process(key):
            continue
        new_value = add_line_breaks(value)
        if new_value != value:
            translations[key] = new_value
            break_applied += 1

    print(f"已加换行: {break_applied} 条")

    # 3. 读取 DAT，按原序替换
    print(f"加载 DAT: {DAT_PATH}")
    dat_entries = read_dat(DAT_PATH)
    print(f"DAT 条目: {len(dat_entries)}")

    replaced = 0
    new_entries = []
    for key, value, crc in dat_entries:
        if key in translations:
            new_entries.append((key, translations[key]))
            replaced += 1
        else:
            new_entries.append((key, value))

    print(f"已替换: {replaced} 条")
    print(f"未匹配: {len(dat_entries) - replaced} 条保持原值")

    # 4. 写入 DAT
    write_dat(DAT_PATH, new_entries)
    print("完成!")


if __name__ == '__main__':
    main()
