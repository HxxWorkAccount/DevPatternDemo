namespace DevPattern.Dev.Universal.CLI {
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;

public class ClientCLI : PromptToolkitCLI
{
    public const int k_port = 57679;  // 随便捏个数

    public override string cliName => "Client CLI";

    public ClientCLI(): base(k_port) { }
}

}
