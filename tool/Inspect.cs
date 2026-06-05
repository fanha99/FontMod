using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

class Inspect
{
    const string Managed = @"F:\Games\PathfinderKingmaker\Kingmaker_Data\Managed";
    static Dictionary<short, OperandType> _ops;

    static void Main(string[] args)
    {
        BuildOps();

        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (s, e) =>
        {
            try
            {
                var name = new AssemblyName(e.Name).Name;
                var p = System.IO.Path.Combine(Managed, name + ".dll");
                if (System.IO.File.Exists(p)) return Assembly.ReflectionOnlyLoadFrom(p);
            }
            catch { }
            try { return Assembly.ReflectionOnlyLoad(e.Name); } catch { return null; }
        };

        var asm = Assembly.ReflectionOnlyLoadFrom(System.IO.Path.Combine(Managed, "Assembly-CSharp.dll"));
        var typeName = args.Length > 0 ? args[0] : "Kingmaker.UI.Common.UIUtility";
        var methodFilter = args.Length > 1 ? args[1] : "Saber";

        var type = asm.GetType(typeName);
        if (type == null) { Console.WriteLine("Khong thay type " + typeName); return; }

        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!m.Name.Contains(methodFilter)) continue;
            Console.WriteLine("==== " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ") ====");
            DumpIL(m);
            Console.WriteLine();
        }
    }

    static void BuildOps()
    {
        _ops = new Dictionary<short, OperandType>();
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var oc = (OpCode)f.GetValue(null);
            _ops[oc.Value] = oc.OperandType;
        }
    }

    static void DumpIL(MethodInfo m)
    {
        MethodBody body;
        try { body = m.GetMethodBody(); } catch (Exception e) { Console.WriteLine("  (khong lay duoc IL: " + e.Message + ")"); return; }
        if (body == null) { Console.WriteLine("  (no body)"); return; }
        var il = body.GetILAsByteArray();
        var mod = m.Module;
        var gargs = m.DeclaringType.GetGenericArguments();
        var margs = m.GetGenericArguments();

        int i = 0;
        while (i < il.Length)
        {
            int opStart = i;
            short code = il[i];
            if (il[i] == 0xFE) { code = (short)(0xFE00 | il[i + 1]); i += 2; }
            else i += 1;

            if (!_ops.TryGetValue(code, out var ot)) { Console.WriteLine($"  IL_{opStart:X4} ??0x{code:X}"); break; }

            switch (ot)
            {
                case OperandType.InlineString:
                    { int tok = BitConverter.ToInt32(il, i); i += 4; string str = "?"; try { str = mod.ResolveString(tok); } catch { } Console.WriteLine($"  IL_{opStart:X4} ldstr  \"{str}\""); break; }
                case OperandType.InlineMethod:
                    { int tok = BitConverter.ToInt32(il, i); i += 4; string nm = "?"; try { var mb = mod.ResolveMethod(tok, gargs, margs); nm = mb.DeclaringType.Name + "." + mb.Name; } catch (Exception e) { nm = "(" + e.GetType().Name + ")"; } Console.WriteLine($"  IL_{opStart:X4} call   {nm}"); break; }
                case OperandType.InlineField:
                    { int tok = BitConverter.ToInt32(il, i); i += 4; string nm = "?"; try { var fb = mod.ResolveField(tok, gargs, margs); nm = fb.DeclaringType.Name + "." + fb.Name; } catch { } Console.WriteLine($"  IL_{opStart:X4} fld    {nm}"); break; }
                case OperandType.ShortInlineR:
                    { float f = BitConverter.ToSingle(il, i); i += 4; Console.WriteLine($"  IL_{opStart:X4} ldc.r4 {f}"); break; }
                case OperandType.InlineR:
                    { double d = BitConverter.ToDouble(il, i); i += 8; Console.WriteLine($"  IL_{opStart:X4} ldc.r8 {d}"); break; }
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: i += 1; break;
                case OperandType.InlineVar: i += 2; break;
                case OperandType.InlineI8: i += 8; break;
                case OperandType.InlineSwitch: { int n = BitConverter.ToInt32(il, i); i += 4 + 4 * n; break; }
                default: i += 4; break; // InlineBrTarget, InlineI, InlineType, InlineTok, InlineSig
            }
        }
    }
}
