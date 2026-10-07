using BioRT.Core.Analysis;
using BioRT.IO.Dicom;
using FellowOakDicom;
using BioRT.Web;
using BioRT.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddFellowOakDicom();
builder.Services.AddSingleton<ModelCatalogService>();
builder.Services.AddSingleton<AnalysisResourceService>();
builder.Services.AddSingleton<DicomBundleImporter>();
builder.Services.AddSingleton<PlanAnalysisService>();

var host = builder.Build();

DicomSetupBuilder.UseServiceProvider(host.Services);

await host.RunAsync();
