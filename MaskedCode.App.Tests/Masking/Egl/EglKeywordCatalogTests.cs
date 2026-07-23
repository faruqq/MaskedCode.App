using MaskedCode.App.Masking.Egl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaskedCode.App.Tests.Masking.Egl
{
    public sealed class EglKeywordCatalogTests
    {
        [Theory]
        [InlineData("package")]
        [InlineData("IMPORT")]
        [InlineData("program")]
        [InlineData("record")]
        [InlineData("function")]
        [InlineData("type")]
        [InlineData("try")]
        [InlineData("onException")]
        [InlineData("set")]
        [InlineData("empty")]
        [InlineData("get")]
        [InlineData("forUpdate")]
        public void
IsKeyword_WithKnownEglKeyword_ShouldReturnTrue(
    string value)
        {
            Assert.True(
                EglKeywordCatalog.IsKeyword(value));
        }

        [Theory]
        [InlineData("BasicProgram")]
        [InlineData("BasicRecord")]
        [InlineData("sqlRecord")]
        [InlineData("char")]
        [InlineData("num")]
        [InlineData("int")]
        [InlineData("smallInt")]
        [InlineData("AnyException")]
        [InlineData("SQLException")]
        public void
        IsBuiltInType_WithKnownEglType_ShouldReturnTrue(
            string value)
        {
            Assert.True(
                EglKeywordCatalog.IsBuiltInType(value));
        }

        [Theory]
        [InlineData("Description")]
        [InlineData("includeReferencedFunctions")]
        [InlineData("allowUnqualifiedItemReferences")]
        [InlineData("throwNrfEofExceptions")]
        [InlineData("handleHardIOErrors")]
        [InlineData("V60ExceptionCompatibility")]
        [InlineData("I4GLItemsNullable")]
        [InlineData("textLiteralDefaultIsString")]
        [InlineData("localSQLScope")]
        [InlineData("tableNames")]
        [InlineData("fieldsMatchColumns")]
        [InlineData("column")]
        [InlineData("sqlVariableLen")]
        public void
IsMetadataProperty_WithKnownProperty_ShouldReturnTrue(
    string value)
        {
            Assert.True(
                EglKeywordCatalog.IsMetadataProperty(value));
        }

        [Fact]
        public void
        IsSystemRoot_WithSysVar_ShouldReturnTrue()
        {
            Assert.True(
                EglKeywordCatalog.IsSystemRoot(
                    "sysVar"));
        }

        [Theory]
        [InlineData("select")]
        [InlineData("FROM")]
        [InlineData("where")]
        [InlineData("and")]
        [InlineData("into")]
        [InlineData("update")]
        [InlineData("set")]
        [InlineData("for")]
        [InlineData("of")]
        public void
IsSqlKeyword_WithKnownSqlKeyword_ShouldReturnTrue(
    string value)
        {
            Assert.True(
                EglKeywordCatalog.IsSqlKeyword(value));
        }

        [Theory]
        [InlineData("MYPROGRAMNAME")]
        [InlineData("MyProgramNameInput")]
        [InlineData("DbMyTableName")]
        [InlineData("MyTableName_Upd01")]
        [InlineData("MY_TABLE_NAME")]
        [InlineData("PARAM1")]
        [InlineData("CorePreMain")]
        [InlineData("HeaderOutput")]
        [InlineData("currentFunctionName")]
        public void
        Catalogs_WithCustomIdentifier_ShouldNotPreserveIdentifierGlobally(
            string value)
        {
            Assert.False(
                EglKeywordCatalog.IsKeyword(value));

            Assert.False(
                EglKeywordCatalog.IsBuiltInType(value));

            Assert.False(
                EglKeywordCatalog.IsMetadataProperty(value));

            Assert.False(
                EglKeywordCatalog.IsSqlKeyword(value));

            Assert.False(
                EglKeywordCatalog.IsSystemRoot(value));
        }
    }
}
