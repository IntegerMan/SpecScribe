import re
data = open('chat.json', encoding='utf-8').read()
for m in re.finditer(r'[Ss]tep\s+C', data):
    i = m.start()
    seg = data[max(0, i-2000):i+3000]
    seg = seg.replace('\\r\\n', '\n').replace('\\n', '\n').replace('\\t', '    ').replace('\\"', '"')
    print('==== match at', i, '====')
    print(seg)
    print('==== end ====')
