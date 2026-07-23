namespace MaskedCode.App.Masking;

internal static class Pl1KeywordCatalog
{
    private static readonly HashSet<string> Keywords = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "ABNORMAL", "ALLOCATE", "AREA", "AUTOMATIC",
        "BASED", "BEGIN", "BINARY", "BIT", "BUILTIN",
        "BY", "CALL", "CHAR", "CHARACTER", "CLOSE",
        "CONDITION", "CONTROLLED",
        "DCL", "DEC", "DECIMAL", "DECLARE", "DEFINED",
        "DIMENSION", "DO", "EDIT", "ELSE", "END",
        "ENTRY", "ENVIRONMENT", "EVENT", "EXTERNAL",
        "FILE", "FINISH", "FIXED", "FLOAT", "FORMAT",
        "FREE", "GET", "GO", "GOTO", "IF", "IN",
        "INCLUDE", "INDEXED", "INIT", "INITIAL", "INPUT",
        "INTERNAL", "KEY", "KEYED", "KEYFROM", "KEYTO",
        "LABEL", "LIKE", "LINE", "LIST", "MAIN",
        "NONVARYING", "ON", "OPEN", "OPTIONS", "OTHERWISE",
        "OUTPUT", "PACKAGE", "PIC", "PICTURE", "POINTER",
        "PRINT", "PROC", "PROCEDURE", "PUT", "READ",
        "RECORD", "REFER", "RETURN", "RETURNS", "REVERT",
        "SELECT", "SIGNAL", "STATIC", "STOP", "STREAM",
        "STRUCTURE", "THEN", "TO", "UNION", "UNTIL",
        "UPDATE", "VARYING", "WHEN", "WHILE", "WRITE"
    };

    public static bool Contains(string value)
    {
        return Keywords.Contains(value);
    }
}