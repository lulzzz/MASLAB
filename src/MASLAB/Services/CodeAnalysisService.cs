﻿using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Hosting;
using System.IO;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace MASLAB.Services
{
    /// <summary>
    /// Serviço de análise C# para o editor de código
    /// </summary>
    public class CodeAnalysisService
    {
        private CodeAnalysisService() { }

        static CodeAnalysisService()
        {
            

            host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
            workspace = new AdhocWorkspace(host); 

            projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "codeAnalysis",
                "CodeAnalysis",
                LanguageNames.CSharp).
                WithMetadataReferences(DefaultReferences
            );
            
        }

        private static string assemblyDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        private static string appDir = Path.GetDirectoryName(typeof(App).Assembly.Location);
        private static readonly IEnumerable<MetadataReference> DefaultReferences =
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(App).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(assemblyDir, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(Path.Combine(appDir, "MathNet.Numerics.dll")),
                MetadataReference.CreateFromFile(Path.Combine(appDir, "FSharp.Core.dll")),
                MetadataReference.CreateFromFile(Path.Combine(appDir, "FParsec.dll")),
                MetadataReference.CreateFromFile(Path.Combine(appDir, "FParsecCS.dll")),
                MetadataReference.CreateFromFile(Path.Combine(appDir, "FSharp.Core.dll"))
            };

        //singleton
        private static CodeAnalysisService service = new CodeAnalysisService();

        private static MefHostServices host;
        private static AdhocWorkspace workspace;
        private static Document document;
        private static string documentCode;
        private static SourceText sourceText;
        private static ProjectInfo projectInfo;




        /// <summary>
        /// Efetua o carregamento do código para a análise no mecanismo Roslyn
        /// </summary>
        /// <param name="code">Código C# a ser analisado</param>
        /// <returns>Instância do serviço de análise</returns>
        public static CodeAnalysisService LoadDocument(string code)
        {
            documentCode = code;
            sourceText = SourceText.From(documentCode);

            workspace.ClearSolution();
            var project = workspace.AddProject(projectInfo);
            document = workspace.AddDocument(project.Id, "CodeAnalysis.cs", sourceText);


            return service;
        }





        /// <summary>
        /// Método utilizado para obter as sugestões do recurso
        /// auto completar do editor de código.
        /// </summary>
        /// <param name="position">Posição atual do cursor</param>
        /// <returns>Conjunto de sugestões para o editor</returns>
        public async Task<IList<ICompletionData>> GetCompletitionDataAt(int position)
        {
            return await Task.Run<IList<ICompletionData>>( async () =>
            {
                var completionService = CompletionService.GetService(document);
                var results = await completionService.GetCompletionsAsync(document, position);

                List<ICompletionData> data = new List<ICompletionData>();

                if (results != null)
                {
                    foreach (var item in results.Items)
                    {
                        data.Add(new CompletionData(item.DisplayText, item.Tags)
                        {
                           ContentDescription = new Lazy<object>(()=> completionService.GetDescriptionAsync(document, item).Result.Text)
                        });
                    }
                }
                
                return data;
            });
        }



        



        /// <summary>
        /// Método utilizado para determinar se o caractere
        /// digitado pelo usuário pode ser utilizado para
        /// abrir a janela de sugestões do editor
        /// </summary>
        /// <param name="position">Posição atual do cursor</param>
        /// <returns>True se o caractere digitado é um gatilho, False do contrário</returns>
        public async Task<bool> ShouldTriggerCompletion(int position)
        {
            return await Task.Run(() =>
            {
                if (position < 1)
                    return false;

                var completionService = CompletionService.GetService(document);
                return completionService.ShouldTriggerCompletion(sourceText, position, CompletionTrigger.CreateInsertionTrigger(documentCode[position - 1]));
            });
        }



        public Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync()
        {
            return Task.Run(() =>
            {
                var tree = SyntaxFactory.ParseSyntaxTree(sourceText, CSharpParseOptions.Default);

                var compilation = CSharpCompilation.Create("MASLAB.Script", 
                    new SyntaxTree[] { tree }, 
                    DefaultReferences,
                    options:new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                return compilation.GetDiagnostics();
            });            
        }


        public class CompletionData : ICompletionData
        {
            public CompletionData(string text, IList<string> tags)
            {
                Text = text;
                Image = Icons.Find(tags);
            }

            public IBitmap Image { get; }

            public string Text { get; }

            // Use this property if you want to show a fancy UIElement in the list.
            public object Content => new TextBlock() { Text = Text };

            internal Lazy<object> ContentDescription { get; set; }

            public object Description {
                get
                {
                    var r = ContentDescription.Value;
                    System.Diagnostics.Debug.WriteLine(r);
                    return r;
                }
            }

            public double Priority { get; } = 0;

            public void Complete(TextArea textArea, ISegment completionSegment,
                EventArgs insertionRequestEventArgs)
            {

                textArea.Document.Replace(completionSegment, Text);
            }
        }
    }
}
