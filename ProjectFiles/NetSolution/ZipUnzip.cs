#region Using directives
using System;
using FTOptix.Core;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.OPCUAServer;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.CoreBase;
using FTOptix.NetLogic;
using System.IO;
using System.IO.Compression;
#endregion

public class ZipUnzip : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void Add()
    {
        var filesList = LogicObject.GetObject("FilesList");
        var filePath = LogicObject.GetVariable("FilePath");
        FTOptix.Core.ResourceUri filePathValue = new FTOptix.Core.ResourceUri(filePath.Value);
        string fileName = filePathValue.Uri;

        var fileEntry = InformationModel.MakeObject<FilesEntry>(Path.GetFileName(fileName));
        fileEntry.FileName = Path.GetFileName(fileName);
        fileEntry.FilePath = filePathValue;
        filesList.Add(fileEntry);
    }

    [ExportMethod]
    public void Remove(string fileName)
    {
        var filesList = LogicObject.GetObject("FilesList");
        foreach (var item in filesList.Children)
        {
            var fileEntry = (FilesEntry)InformationModel.GetObject(item.NodeId);
            if (fileEntry.FileName == fileName)
                filesList.Remove(item);
        }
    }

    [ExportMethod]
    public void CreateZip(string zipName)
    {
        string zipFileName;
        string fileName = "";
        FTOptix.Core.ResourceUri filePathValue = null;
        var filePath = LogicObject.GetVariable("FilePath");
        filePathValue = new FTOptix.Core.ResourceUri(filePath.Value);
        fileName = filePathValue.Uri;

        zipFileName = fileName.Replace(Path.GetFileName(fileName), zipName);
        var filesList = LogicObject.GetObject("FilesList");

        using (var zipArchive = ZipFile.Open(zipFileName, ZipArchiveMode.Update))
        {
            foreach (var item in filesList.Children)
            {
                var fileEntry = (FilesEntry)InformationModel.GetObject(item.NodeId);
                var pathValue = new FTOptix.Core.ResourceUri(fileEntry.FilePath);
                var fileInfo = new FileInfo(pathValue.Uri);
                zipArchive.CreateEntryFromFile(fileInfo.FullName, fileInfo.Name);
            }
        }
    }

    [ExportMethod]
    public void ExtractZipFile()
    {
        string fileName = "";
        FTOptix.Core.ResourceUri filePathValue = null;
        var filePath = LogicObject.GetVariable("FilePath");
        filePathValue = new FTOptix.Core.ResourceUri(filePath.Value);
        fileName = filePathValue.Uri;

        ZipFile.ExtractToDirectory(fileName, Path.GetDirectoryName(fileName));
    }
}
