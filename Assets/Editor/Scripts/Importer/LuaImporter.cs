namespace DevPattern.Editor.Importer {

#if UNITY_2020_2_OR_NEWER

using UnityEditor.AssetImporters;
using UnityEngine;
using System.IO;

[ScriptedImporter(1, "lua")]
public class LuaImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx) {
        // 读取 .lua 文件的文本内容
        string scriptText = File.ReadAllText(ctx.assetPath);

        // 创建一个 TextAsset
        TextAsset textAsset = new TextAsset(scriptText);

        // 将这个 TextAsset 添加到导入结果中，Unity 会把它当作主要对象
        ctx.AddObjectToAsset("main obj", textAsset);
        ctx.SetMainObject(textAsset);
    }
}

#endif

}
