namespace MaskedCode.App.Masking;

internal static class EglKeywordCatalog
{
    private static readonly HashSet<string> Keywords = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "ADD",
        "AS",
        "BY",
        "CALL",
        "CASE",
        "CLOSE",
        "CONTINUE",
        "CURRENT",
        "DELETE",
        "ELSE",
        "EMPTY",
        "END",
        "EXECUTE",
        "EXIT",
        "EXTERNAL",
        "FOR",
        "FOREACH",
        "FORUPDATE",
        "FORWARD",
        "FROM",
        "FUNCTION",
        "GET",
        "IF",
        "IMPORT",
        "IN",
        "INOUT",
        "INTERFACE",
        "INTO",
        "LIBRARY",
        "NEW",
        "NO",
        "ONEXCEPTION",
        "OPEN",
        "OTHERWISE",
        "OUT",
        "PACKAGE",
        "PRIVATE",
        "PROGRAM",
        "PUBLIC",
        "RECORD",
        "REPLACE",
        "RETURN",
        "RETURNS",
        "SERVICE",
        "SET",
        "THROW",
        "TO",
        "TRY",
        "TYPE",
        "USE",
        "USING",
        "WHEN",
        "WHILE",
        "WITH",
        "YES"
    };

    private static readonly HashSet<string> BuiltInTypes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "ANY",
        "ANYEXCEPTION",
        "BASICPROGRAM",
        "BASICRECORD",
        "BIGINT",
        "BOOLEAN",
        "CHAR",
        "DATE",
        "DECIMAL",
        "FLOAT",
        "INT",
        "INTERVAL",
        "MONEY",
        "NUM",
        "SMALLFLOAT",
        "SMALLINT",
        "SQLDATE",
        "SQLEXCEPTION",
        "SQLRECORD",
        "SQLTIME",
        "SQLTIMESTAMP",
        "STRING",
        "TIME",
        "TIMESTAMP"
    };

    private static readonly HashSet<string>
        MetadataProperties = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "ALLOWUNQUALIFIEDITEMREFERENCES",
            "COLUMN",
            "DESCRIPTION",
            "FIELDSMATCHCOLUMNS",
            "HANDLEHARDIOERRORS",
            "I4GLITEMSNULLABLE",
            "INCLUDEREFERENCEDFUNCTIONS",
            "LOCALSQLSCOPE",
            "SQLVARIABLELEN",
            "TABLENAMES",
            "TEXTLITERALDEFAULTISSTRING",
            "THROWNRFEOFEXCEPTIONS",
            "V60EXCEPTIONCOMPATIBILITY"
        };

    private static readonly HashSet<string> SqlKeywords = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "ALL",
        "AND",
        "AS",
        "ASC",
        "BETWEEN",
        "BY",
        "CASE",
        "DELETE",
        "DESC",
        "DISTINCT",
        "ELSE",
        "END",
        "EXISTS",
        "FOR",
        "FROM",
        "FULL",
        "GROUP",
        "HAVING",
        "IN",
        "INNER",
        "INSERT",
        "INTO",
        "IS",
        "JOIN",
        "LEFT",
        "LIKE",
        "NOT",
        "NULL",
        "OF",
        "ON",
        "OR",
        "ORDER",
        "OUTER",
        "RIGHT",
        "SELECT",
        "SET",
        "THEN",
        "UNION",
        "UPDATE",
        "VALUES",
        "WHEN",
        "WHERE"
    };

    private static readonly HashSet<string> SystemRoots = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "SYSVAR"
    };

    private static readonly HashSet<string> Directives = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "DOC",
        "SQL"
    };

    private static readonly HashSet<string> EntryPointNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "MAIN"
    };

    public static bool IsKeyword(string value)
    {
        return Keywords.Contains(value);
    }

    public static bool IsBuiltInType(string value)
    {
        return BuiltInTypes.Contains(value);
    }

    public static bool IsMetadataProperty(string value)
    {
        return MetadataProperties.Contains(value);
    }

    public static bool IsSqlKeyword(string value)
    {
        return SqlKeywords.Contains(value);
    }

    public static bool IsSystemRoot(string value)
    {
        return SystemRoots.Contains(value);
    }

    public static bool IsDirective(string value)
    {
        return Directives.Contains(value);
    }

    public static bool IsEntryPointName(string value)
    {
        return EntryPointNames.Contains(value);
    }

    public static bool IsReservedCandidate(string value)
    {
        return IsKeyword(value) ||
               IsBuiltInType(value) ||
               IsMetadataProperty(value) ||
               IsSqlKeyword(value) ||
               IsSystemRoot(value) ||
               IsDirective(value) ||
               IsEntryPointName(value);
    }
}