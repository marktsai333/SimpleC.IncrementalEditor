using System.Collections.Generic;

namespace SimpleC.IncrementalEditor.Models;

public class DirtyQueue
{
    private Queue<int> queue = new();
    public void Enqueue(int id) => queue.Enqueue(id);
    public int? Dequeue() => queue.Count > 0 ? queue.Dequeue() : null;
    public bool IsEmpty => queue.Count == 0;
}