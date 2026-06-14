using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

class FindCallers
{
    const string Managed = @"F:\Games\PathfinderKingmaker\Kingmaker_Data\Managed";
    static Dictionary<short, OperandType> _ops;

    static void Main(string[] args)
    {
        BuildOps();
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (s, e) =>
        {
            try { var name = new AssemblyName(e.Name).Name; var p = System.IO.Path.Combine(Managed, name + ".dll"); if (System.IO.File.Exists(p)) return Assembly.ReflectionOnlyLoadFrom(p); } catch { }
            try { return Assembly.ReflectionOnlyLoad(e.Name); } catch { return null; }
        };
        var asm = Assembly.ReflectionOnlyLoadFrom(System.IO.Path.Combine(Managed, "Assembly-CSharp.dll"));
        string target = args.Length > 0 ? args[0] : "GetSaberBookFormat";

        Type[] allTypes;
        try { allTypes = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { allTypes = ex.Types.Where(t => t != null).ToArray(); }
        foreach (var type in allTypes)
        {
            if (type == null) continue;
            MethodInfo[] methods;
            try { methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly); }
            catch { continue; }
            foreach (var m in methods)
            {
                MethodBody body;
                try { body = m.GetMethodBody(); } catch { continue; }
                if (body == null) continue;
                byte[] il;
                try { il = body.GetILAsByteArray(); } catch { continue; }
                var mod = m.Module;
                var gargs = type.GetGenericArguments();
                var margs = m.GetGenericArguments();
                int i = 0;
                bool hit = false;
                while (i < il.Length)
                {
                    short code = il[i];
                    if (il[i] == 0xFE) { code = (short)(0xFE00 | il[i + 1]); i += 2; } else i += 1;
                    if (!_ops.TryGetValue(code, out var ot)) break;
                    if (ot == OperandType.InlineMethod)
                    {
                        int tok = BitConverter.ToInt32(il, i);
                        try { var mb = mod.ResolveMethod(tok, gargs, margs); if (mb != null && mb.Name == target) hit = true; } catch { }
                        i += 4;
                    }
                    else i += OperandSize(ot, il, i);
                }
                if (hit) Console.WriteLine(type.FullName + " :: " + m.Name);
            }
        }
    }

    static int OperandSize(OperandType ot, byte[] il, int i)
    {
        switch (ot)
        {
            case OperandType.InlineNone: return 0;
            case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: return 1;
            case OperandType.InlineVar: return 2;
            case OperandType.InlineI8: case OperandType.InlineR: return 8;
            case OperandType.InlineSwitch: { int n = BitConverter.ToInt32(il, i); return 4 + 4 * n; }
            default: return 4;
        }
    }

    static void BuildOps()
    {
        _ops = new Dictionary<short, OperandType>();
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        { var oc = (OpCode)f.GetValue(null); _ops[oc.Value] = oc.OperandType; }
    }
}
