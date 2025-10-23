namespace gmlp.Tests;

public class CriticalPriorityTest() : LanguageTest("CriticalPriority")
{
    
    
    public override string GetCode() {
        return 
"""
aaaaa;
bbbbb;
ccccc;
ddddd;
""";
    }

    public override bool[] GetPatchesCritical()
    {
        return
        [
            false,
            true,
            
            false,
            false,
        ];
    }
    public override string[] GetPatchSections() {
        return [
"""
find_line_with('bb')
write_replace('hhhhh;')
""",
"""
find_line_with('bbbb')
write_replace('jjjjj;')
""",
"""
find_line_with('cccc')
write_replace('fffff;')
""",
"""
find_line_with('cccc')
write_replace('ggggg;')
""",
        ];
    }
    public override string GetExpected() {
        return 
"""
aaaaa;
jjjjj;
fffff;
ddddd;
""";
    }
}