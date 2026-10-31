# Minimal Direct3D9 SM2/SM3 bytecode disassembler (clean-room, from the public token format).
# Usage: python3 sm3dis.py blob.bin [--grep KEYWORD]
# Finds every vs_/ps_ program in the blob, prints its keyword context (nearby strings),
# constant table (CTAB) and instructions.
import struct, sys, re

OPS = {0:'nop',1:'mov',2:'add',3:'sub',4:'mad',5:'mul',6:'rcp',7:'rsq',8:'dp3',9:'dp4',10:'min',11:'max',
       12:'slt',13:'sge',14:'exp',15:'log',16:'lit',17:'dst',18:'lrp',19:'frc',20:'m4x4',21:'m4x3',22:'m3x4',
       23:'m3x3',24:'m3x2',25:'call',26:'callnz',27:'loop',28:'ret',29:'endloop',30:'label',31:'dcl',32:'pow',
       33:'crs',34:'sgn',35:'abs',36:'nrm',37:'sincos',38:'rep',39:'endrep',40:'if',41:'ifc',42:'else',43:'endif',
       44:'break',45:'breakc',46:'mova',47:'defb',48:'defi',64:'texcoord',65:'texkill',66:'texld',
       81:'def',88:'cmp',89:'dp2add',90:'dsx',91:'dsy',92:'texldd',93:'setp',94:'texldl',95:'breakp'}
REG = {0:'r',1:'v',2:'c',3:'a/t',4:'rasout',5:'attrout',6:'o',7:'input',8:'oC',9:'oDepth',10:'s',
       11:'c',12:'c',13:'c',14:'b',15:'aL',16:'tempfloat16',17:'misc',18:'label',19:'p'}
SRCMOD = {0:'{}',1:'-{}',2:'{}_bias',3:'-{}_bias',4:'{}_bx2',5:'-{}_bx2',6:'{}_x2',7:'-{}_x2',8:'{}_dz',
          9:'{}_dw',10:'abs({})',11:'-abs({})',12:'!{}'}

def regname(tok, consts):
    t = ((tok >> 28) & 7) | (((tok >> 11) & 3) << 3); n = tok & 0x7FF
    if t == 2 and n in consts: return consts[n]
    if t == 2: return 'c%d' % n
    if t == 10: return 's%d' % n
    if t == 17: return 'vPos' if n == 0 else 'vFace'
    return '%s%d' % (REG.get(t, '?%d' % t), n)

def swz(tok):
    s = (tok >> 16) & 0xFF
    comp = ''.join('xyzw'[(s >> (2 * i)) & 3] for i in range(4))
    if comp == 'xyzw': return ''
    if len(set(comp)) == 1: return '.' + comp[0]
    return '.' + comp

def mask(tok):
    m = (tok >> 16) & 0xF
    return '' if m == 0xF else '.' + ''.join(c for i, c in enumerate('xyzw') if m & (1 << i))

def ctab(words):
    # words: comment payload as bytes starting with 'CTAB'
    b = words
    if b[:4] != b'CTAB': return {}
    base = 4
    hdr = struct.unpack_from('<7I', b, base)
    nconst, cinfo = hdr[3], hdr[4]
    out = {}
    for i in range(nconst):
        name_off, regset, regidx, regcnt, _, _ = struct.unpack_from('<IHHHHI', b, base + cinfo + i * 20)
        name = b[base + name_off:b.index(b'\0', base + name_off)].decode('latin1')
        if regset == 3: name = 'smp:' + name     # sampler
        for j in range(regcnt):
            out[(regset, regidx + j)] = name + ('[%d]' % j if regcnt > 1 else '')
    return out

def disasm(data, start):
    ver = struct.unpack_from('<I', data, start)[0]
    kind = 'ps' if (ver >> 16) == 0xFFFF else 'vs'
    lines = ['%s_%d_%d' % (kind, (ver >> 8) & 0xFF, ver & 0xFF)]
    p = start + 4; consts = {}; samplers = {}
    while p + 4 <= len(data):
        tok = struct.unpack_from('<I', data, p)[0]
        if tok == 0x0000FFFF: return '\n'.join(lines), p + 4
        op = tok & 0xFFFF
        if op == 0xFFFE:
            n = (tok >> 16) & 0x7FFF
            ct = ctab(data[p + 4:p + 4 + n * 4])
            for (rs, ri), nm in ct.items():
                if rs == 3: samplers[ri] = nm
                else: consts[ri] = nm
            if ct: lines.append('  // CTAB: ' + ', '.join('%s=%s%d' % (v, 's' if k[0] == 3 else 'c', k[1]) for k, v in sorted(ct.items(), key=lambda x: (x[0][0], x[0][1]))))
            p += 4 + n * 4; continue
        ln = (tok >> 24) & 0xF
        args = list(struct.unpack_from('<%dI' % ln, data, p + 4)); p += 4 + ln * 4
        name = OPS.get(op, 'op%d' % op)
        if (tok >> 30) & 1: name += '_pp'      # not exact (predication/coissue) but rare
        if op == 81:   # def cN, f, f, f, f
            reg = args[0] & 0x7FF; vals = struct.unpack('<4f', struct.pack('<4I', *args[1:5]))
            lines.append('  def c%d, %s' % (reg, ', '.join('%g' % v for v in vals))); consts.setdefault(reg, 'c%d' % reg); continue
        if op == 31:   # dcl
            usage = args[0] & 0x1F; idx = (args[0] >> 16) & 0xF
            u = ['position','blendweight','blendindices','normal','psize','texcoord','tangent','binormal','tessfactor','positiont','color','fog','depth','sample'][usage] if usage < 14 else '?'
            lines.append('  dcl_%s%d %s%s' % (u, idx, regname(args[1], {}), mask(args[1]))); continue
        if not args: lines.append('  ' + name); continue
        dst = args[0]
        dmod = (dst >> 20) & 0xF
        sat = '_sat' if dmod & 1 else ''
        ops = [regname(dst, consts) + mask(dst)]
        for s in args[1:]:
            r = regname(s, consts) if not (((s >> 28) & 7) | (((s >> 11) & 3) << 3)) == 10 else samplers.get(s & 0x7FF, 's%d' % (s & 0x7FF))
            ops.append(SRCMOD.get((s >> 24) & 0xF, '?{}').format(r + swz(s)))
        if op in (40, 41, 42, 43, 38, 39, 27, 29, 45): ops = [regname(a, consts) + swz(a) for a in args]
        lines.append('  %s%s %s' % (name, sat, ', '.join(ops)))
    return '\n'.join(lines), p

if __name__ == '__main__':
    data = open(sys.argv[1], 'rb').read()
    want = sys.argv[sys.argv.index('--grep') + 1] if '--grep' in sys.argv else None
    want_kind = sys.argv[sys.argv.index('--kind') + 1] if '--kind' in sys.argv else None
    # programs start with a version token; a preceding block holds keyword strings
    starts = [m.start() for m in re.finditer(b'\x00\x03\xff\xff|\x00\x03\xfe\xff', data)]
    prev = 0; n = 0
    for s in starts:
        if s < prev: continue
        ctx = data[max(prev, s - 400):s]
        kws = sorted(set(w.decode() for w in re.findall(rb'[A-Z][A-Z0-9_]{3,}', ctx)))
        try:
            text, end = disasm(data, s)
        except Exception as e:
            prev = s + 4; continue
        prev = end
        if want and want not in ' '.join(kws): continue
        if want_kind and not text.startswith(want_kind): continue
        n += 1
        print('=== program %d @%d keywords=%s' % (n, s, ' '.join(kws)))
        print(text)
