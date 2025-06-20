using System.Collections.Generic;

namespace SimpleC.IncrementalEditor.Models;

public class CFGNode
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public List<CFGNode> Edges { get; set; } = new();
}