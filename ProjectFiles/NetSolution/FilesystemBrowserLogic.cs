#region StandardUsing
using System;
using FTOptix.HMIProject;
using UAManagedCore;
using FTOptix.NetLogic;
using FTOptix.Core;
using System.IO;
using System.Linq;
using System.Collections.Generic;
#endregion

public class FilesystemBrowserLogic : BaseNetLogic
{
	public override void Start()
	{
		relativePathsNamespacePrefix = $"ns={LogicObject.NodeId.NamespaceIndex}";

		pathVariable = LogicObject.GetVariable("Path");
		if (pathVariable == null)
			throw new CoreConfigurationException("Path variable not found in FilesystemBrowserLogic");

		filterVariable = Owner.GetVariable("ExtensionFilter");
		if (filterVariable == null)
			throw new CoreConfigurationException("ExtensionFilter variable not found in FilesystemBrowserLogic");

		// Fix Path variable non-legal values, i.e. non-existing USB or invalid path (not starting with %PROJECTDIR%\ or %APPLICATIONDIR%\)
		var startFolderPathResourceUri = FixPathVariableIfNecessary();
		Browse(startFolderPathResourceUri.Uri);

		pathVariable.VariableChange += PathVariable_VariableChange;
	}

	public override void Stop()
	{
		pathVariable.VariableChange -= PathVariable_VariableChange;
	}

	private void PathVariable_VariableChange(object sender, VariableChangeEventArgs e)
	{
		var updatedPathResourceUri = new ResourceUri(e.NewValue);
		if (!IsRelativeResourceUri(updatedPathResourceUri))
			throw new CoreConfigurationException($"Path variable must start with: '%PROJECTDIR%\\', '%APPLICATIONDIR%\\' or '%USB<n>%'");

		Browse(updatedPathResourceUri.Uri);
	}

	[ExportMethod]
	public void Browse(string path)
	{
		if (path == string.Empty)
		{
			Log.Warning("FilesystemBrowserLogic", "Path variable is empty");
			return;
		}

		if (!Directory.Exists(path))
		{
			Log.Warning("FilesystemBrowserLogic", $"Path '{path}' does not exist");
			return;
		}

		var currentDirectory = new DirectoryInfo(@path);
		var filesList = LogicObject.GetObject("FilesList");
		if (filesList == null)
			return;

		// Clean files list
		filesList.Children.ToList().ForEach((entry) => entry.Delete());

		// Create back entry
		var backEntry = InformationModel.MakeObject<FileEntry>("back");
		backEntry.FileName = "..";
		backEntry.IsDirectory = true;
		filesList.Add(backEntry);

		string extensions = filterVariable.Value;
		var extensionsList = extensions.Split(';').ToList();

		var directories = currentDirectory.GetFileSystemInfos().Where(entry => entry is DirectoryInfo);

		foreach (var dir in directories)
		{
			var fileSystemEntry = CreateFilesystemEntry(dir, true);
			filesList.Add(fileSystemEntry);
		}

		var files = currentDirectory.GetFileSystemInfos().Where(entry => entry is FileInfo);

		foreach (var file in files)
		{
			if (!AllFilesFilterSelected(extensionsList) && FileHasToBeFiltered(extensionsList, file))
				continue;

			var fileSystemEntry = CreateFilesystemEntry(file, false);
			filesList.Add(fileSystemEntry);
		}

		return;
	}

	private bool FileHasToBeFiltered(List<string> extensionsList, FileSystemInfo file)
	{
		return !extensionsList.Contains($"*{file.Extension}");
	}

	// All files are shown if the filter is empty or "*.*" is present in the filter list
	private bool AllFilesFilterSelected(List<string> extensionsList)
	{
		return extensionsList.Contains("*.*") || (extensionsList.Count == 1 && extensionsList.Contains(string.Empty));
	}

	private ResourceUri FixPathVariableIfNecessary()
	{
		var startFolderPathResourceUri = new ResourceUri(pathVariable.Value);

		// Check that PathVariable has a legal value, i.e. it is a value that:
		// - is a relative path
		// - in case of USB devices it represents a connected USB
		// In case of wrong value, the %PROJECTDIR% folder is taken as the default value
		var defaultResourceUri = new ResourceUri($"{relativePathsNamespacePrefix};%PROJECTDIR%\\");
		if (!IsRelativeResourceUri(startFolderPathResourceUri))
		{
			Log.Error("Path variable must start with: '%PROJECTDIR%\\', '%APPLICATIONDIR%\\' or '%USB<n>%'. Fallback to '%PROJECTDIR%\\'");
			pathVariable.Value = defaultResourceUri;
			return defaultResourceUri;
		}

		// Check that the start folder resource uri can be resolved (i.e. non-existing USB device)
		try
		{
			var uri = startFolderPathResourceUri.Uri;
		}
		catch (Exception exception)
		{
			Log.Error("FilesystemBrowserLogic", $"Path variable '{pathVariable.Value.Value}' not found: {exception.Message}. Falling back to '%PROJECTDIR%\\'");
			pathVariable.Value = defaultResourceUri;
			return defaultResourceUri;
		}

		return startFolderPathResourceUri;
	}

	private bool IsRelativeResourceUri(ResourceUri resourceUri)
	{
		return resourceUri.UriType == UriType.ApplicationRelative ||
			resourceUri.UriType == UriType.ProjectRelative ||
			resourceUri.UriType == UriType.USBRelative;
	}

	private FileEntry CreateFilesystemEntry(FileSystemInfo entry, bool isDirectory)
	{
		var fileSystemEntry = InformationModel.MakeObject<FileEntry>(entry.Name);
		fileSystemEntry.FileName = entry.Name;
		fileSystemEntry.IsDirectory = isDirectory;
		if (!isDirectory)
		{
			var file = entry as FileInfo;
			fileSystemEntry.Size = (ulong)Math.Round(file.Length / 1000.0);
		}

		return fileSystemEntry;

	}

	private IUAVariable pathVariable;
	private IUAVariable filterVariable;

	private static string relativePathsNamespacePrefix;
}
