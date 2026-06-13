extern alias GitHubCopilotSdk;
using System;
using System.Linq;
using CopilotSdk = GitHubCopilotSdk::GitHub.Copilot;
foreach(var p in typeof(CopilotSdk.SessionConfig).GetProperties()) Console.WriteLine(p.Name+" : "+p.PropertyType.FullName);
