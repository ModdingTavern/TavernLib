using System.Collections.Generic;

namespace TavernLib.ModulePorts;

// A console answer that reaches TavernKeeper as a JSON array and a text console as one line per
// item. A plain IEnumerable answers its type name as text.
public class Lines<T>(IEnumerable<T> items) : List<T>(items)
{
    public override string ToString() => Count == 0 ? "None" : string.Join("\n", this);
}
