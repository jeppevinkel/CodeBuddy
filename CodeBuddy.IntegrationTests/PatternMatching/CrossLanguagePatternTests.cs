using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CodeBuddy.Core.Implementation.PatternDetection;
using CodeBuddy.Core.Implementation.CodeValidation.AST;
using CodeBuddy.Core.Models.Patterns;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.IntegrationTests.PatternMatching
{
    [TestClass]
    public class CrossLanguagePatternTests
    {
        private IPatternMatchingEngine _patternEngine;
        private IPatternRepository _patternRepository;
        private ICSharpASTConverter _csharpConverter;
        private IPythonASTConverter _pythonConverter;
        private IJavaScriptASTConverter _jsConverter;

        [TestInitialize]
        public void Setup()
        {
            _patternRepository = new PatternRepository("integration_patterns");
            _patternEngine = new PatternMatchingEngine(_patternRepository);
            _csharpConverter = new CSharpASTConverter();
            _pythonConverter = new PythonASTConverter();
            _jsConverter = new JavaScriptASTConverter();
        }

        [TestMethod]
        public async Task DetectPatterns_ResourceLeakPattern_AcrossLanguages()
        {
            // Test data in different languages with the same pattern
            var csharpCode = @"
                using var file = File.OpenRead(""test.txt"");
                // Missing disposal
                var data = new byte[1024];
                file.Read(data, 0, data.Length);
            ";

            var pythonCode = @"
                file = open('test.txt', 'r')
                # Missing with statement or close()
                data = file.read()
            ";

            var jsCode = @"
                const fs = require('fs');
                const file = fs.openSync('test.txt', 'r');
                // Missing close
                const data = fs.readFileSync(file);
            ";

            // Convert code to Unified AST
            var csharpAst = await _csharpConverter.ConvertToUnifiedASTAsync(csharpCode);
            var pythonAst = await _pythonConverter.ConvertToUnifiedASTAsync(pythonCode);
            var jsAst = await _jsConverter.ConvertToUnifiedASTAsync(jsCode);

            // Detect patterns in each language
            var csharpResults = await _patternEngine.DetectPatternsAsync(csharpAst, "test.cs");
            var pythonResults = await _patternEngine.DetectPatternsAsync(pythonAst, "test.py");
            var jsResults = await _patternEngine.DetectPatternsAsync(jsAst, "test.js");

            // Assert pattern detection works consistently across languages
            Assert.IsTrue(csharpResults.Exists(r => r.PatternId == "RESMGMT001"));
            Assert.IsTrue(pythonResults.Exists(r => r.PatternId == "RESMGMT001"));
            Assert.IsTrue(jsResults.Exists(r => r.PatternId == "RESMGMT001"));
        }

        [TestMethod]
        public async Task DetectPatterns_ComplexPattern_CrossFileDetection()
        {
            // Test files with interdependent patterns
            var csharpMainCode = @"
                public class DataService {
                    private readonly IRepository _repo;
                    public async Task ProcessData() {
                        var data = await _repo.GetDataAsync();
                        // Complex processing
                    }
                }
            ";

            var pythonHelperCode = @"
                class Repository:
                    def get_data_async(self):
                        # Async implementation
                        return await fetch_data()
            ";

            var jsClientCode = @"
                class DataClient {
                    constructor(service) {
                        this.service = service;
                    }
                    async fetchData() {
                        await this.service.processData();
                    }
                }
            ";

            // Convert and combine ASTs
            var combinedAst = new UnifiedASTNode();
            combinedAst.Children.Add(await _csharpConverter.ConvertToUnifiedASTAsync(csharpMainCode));
            combinedAst.Children.Add(await _pythonConverter.ConvertToUnifiedASTAsync(pythonHelperCode));
            combinedAst.Children.Add(await _jsConverter.ConvertToUnifiedASTAsync(jsClientCode));

            // Detect cross-file patterns
            var results = await _patternEngine.DetectPatternsAsync(combinedAst, "multifile_test");

            // Assert complex pattern detection
            Assert.IsTrue(results.Exists(r => r.PatternId == "ARCH001")); // Architecture pattern
            Assert.IsTrue(results.Exists(r => r.PatternId == "ASYNC001")); // Async pattern
        }

        [TestMethod]
        public async Task DetectPatterns_SecurityPattern_LanguageSpecificSyntax()
        {
            // Test security pattern detection with language-specific syntax
            var csharpCode = @"
                var query = $""SELECT * FROM Users WHERE Id = {userId}"";
                using var cmd = new SqlCommand(query, connection);
            ";

            var pythonCode = @"
                query = f""SELECT * FROM Users WHERE Id = {user_id}""
                cursor.execute(query)
            ";

            var jsCode = @"
                const query = `SELECT * FROM Users WHERE Id = ${userId}`;
                connection.query(query);
            ";

            // Convert to AST and detect patterns
            var csharpAst = await _csharpConverter.ConvertToUnifiedASTAsync(csharpCode);
            var pythonAst = await _pythonConverter.ConvertToUnifiedASTAsync(pythonCode);
            var jsAst = await _jsConverter.ConvertToUnifiedASTAsync(jsCode);

            var csharpResults = await _patternEngine.DetectPatternsAsync(csharpAst, "test.cs");
            var pythonResults = await _patternEngine.DetectPatternsAsync(pythonAst, "test.py");
            var jsResults = await _patternEngine.DetectPatternsAsync(jsAst, "test.js");

            // Assert SQL injection pattern detection
            Assert.IsTrue(csharpResults.Exists(r => r.PatternId == "SEC001"));
            Assert.IsTrue(pythonResults.Exists(r => r.PatternId == "SEC001"));
            Assert.IsTrue(jsResults.Exists(r => r.PatternId == "SEC001"));

            // Verify confidence scores
            Assert.IsTrue(csharpResults.Find(r => r.PatternId == "SEC001").ConfidenceScore > 0.8);
            Assert.IsTrue(pythonResults.Find(r => r.PatternId == "SEC001").ConfidenceScore > 0.8);
            Assert.IsTrue(jsResults.Find(r => r.PatternId == "SEC001").ConfidenceScore > 0.8);
        }

        [TestMethod]
        public async Task DetectPatterns_PerformancePattern_LargeCodebase()
        {
            // Generate large codebase with nested loops
            var codeBlocks = GenerateLargeCodebase(1000); // 1000 code blocks
            var startTime = DateTime.UtcNow;

            foreach (var block in codeBlocks)
            {
                var ast = await _csharpConverter.ConvertToUnifiedASTAsync(block);
                var results = await _patternEngine.DetectPatternsAsync(ast, "perf_test.cs");
                
                // Verify pattern detection
                Assert.IsTrue(results.Exists(r => r.PatternId == "PERF001"));
            }

            var duration = DateTime.UtcNow - startTime;
            
            // Performance validation
            Assert.IsTrue(duration.TotalSeconds < 30); // Should process within 30 seconds
        }

        [TestMethod]
        public async Task DetectPatterns_ErrorHandling_EdgeCases()
        {
            // Test pattern detection with invalid or edge case inputs
            var invalidCode = "}} invalid {{ code";
            var emptyCode = "";
            var hugeCode = new string('a', 1_000_000); // 1MB of code

            // Test invalid code
            var ast = await _csharpConverter.ConvertToUnifiedASTAsync(invalidCode);
            var results = await _patternEngine.DetectPatternsAsync(ast, "invalid.cs");
            Assert.IsFalse(results.Any()); // Should not crash, return empty results

            // Test empty code
            ast = await _csharpConverter.ConvertToUnifiedASTAsync(emptyCode);
            results = await _patternEngine.DetectPatternsAsync(ast, "empty.cs");
            Assert.IsFalse(results.Any());

            // Test huge code file
            ast = await _csharpConverter.ConvertToUnifiedASTAsync(hugeCode);
            results = await _patternEngine.DetectPatternsAsync(ast, "huge.cs");
            // Should handle large files without out of memory
            Assert.IsNotNull(results);
        }

        private List<string> GenerateLargeCodebase(int blockCount)
        {
            var codeBlocks = new List<string>();
            for (int i = 0; i < blockCount; i++)
            {
                codeBlocks.Add(@$"
                    public void ProcessData_{i}() {{
                        for (int x = 0; x < 100; x++) {{
                            for (int y = 0; y < 100; y++) {{
                                // Nested loop pattern
                            }}
                        }}
                    }}
                ");
            }
            return codeBlocks;
        }
    }
}