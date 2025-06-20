namespace SimpleC.IncrementalEditor.Models;

public enum AnomalyType { UR, DU, DD }

public class Anomaly
{
    public AnomalyType Type { get; set; }
    public int Line { get; set; }
    public string Var { get; set; } = "";
    public string Message => $"{Type} on line {Line}: {Var}";
}