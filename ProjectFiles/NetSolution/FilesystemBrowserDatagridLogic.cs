#region StandardUsing
using FTOptix.Core;
using UAManagedCore;
using FTOptix.NetLogic;
using System.IO;
using System;
#endregion

public class FilesystemBrowserDatagridLogic : BaseNetLogic
{
	public override void Start()
	{
		relativePathsNamespacePrefix = $"ns={LogicObject.NodeId.NamespaceIndex}";
		pathVariable = Owner.Owner.GetVariable("FolderPath");
		if (pathVariable == null)
			throw new CoreConfigurationException("FolderPath variable not found in FilesystemBrowser");

		fullPathVariable = Owner.Owner.GetVariable("FullPath");
		if (fullPathVariable == null)
			throw new CoreConfigurationException( "FullPath variable not found in FilesystemBrowser");

		selectedItemVariable = Owner.GetVariable("SelectedItem");
		selectedItemVariable.VariableChange += SelectedItemVariable_VariableChange;
	}

	public override void Stop()
	{
		selectedItemVariable.VariableChange -= SelectedItemVariable_VariableChange;
	}

	private void SelectedItemVariable_VariableChange(object sender, VariableChangeEventArgs e)
	{
		var entry = (FileEntry)LogicObject.Context.GetObject(e.NewValue);
		if (entry == null)
			return;

		UpdateCurrentPath(entry.FileName);
	}

	[ExportMethod]
	public void UpdateCurrentPath(string lastPathToken)
	{
		// Necessary when QStudio placeholder path is configured with only %APPLICATIONDIR%\, %PROJECTDIR%\ (i.e. at the start of the project)
		var currentPathResourceUri = new ResourceUri(AddNamespacePrefixIfNecessary(pathVariable.Value));
		if (lastPathToken == ".." && IsTopLevelFolder(currentPathResourceUri))
		{
			Log.Warning("FilesystemBrowserDataGridLogic", $"Cannot browse to parent of '{pathVariable.Value.Value}' since it is a top-level folder");
			return;
		}

		var currentPath = currentPathResourceUri.Uri;
		if (lastPathToken == "..")
		{
			DirectoryInfo parentDirectory = Directory.GetParent(currentPath);
			if (parentDirectory == null)
				return;

			var parentDirectoryPath = parentDirectory.FullName;

			pathVariable.Value = ConvertAbsolutePathToRelativeResourceUriString(currentPathResourceUri, parentDirectoryPath);
			fullPathVariable.Value = ResourceUri.FromAbsoluteFilePath(parentDirectoryPath);
			return;
		}

		currentPath = Path.Combine(currentPath, lastPathToken);
		fullPathVariable.Value = ResourceUri.FromAbsoluteFilePath(currentPath);

		if (!IsDirectory(currentPath))
			return;

		pathVariable.Value = ConvertAbsolutePathToRelativeResourceUriString(currentPathResourceUri, currentPath);
	}

	// Convert the full path (e.g. D:\\MyFolder\\SubFolder) to a string with QStudio location placeholder followed by the relative path (e.g. %USB1%/MyFolder\\SubFolder)
	// A string with this format can then be parsed back into a ResourceUri with the corresponding UriType (e.g. UriType.USBRelative).
	private string ConvertAbsolutePathToRelativeResourceUriString(ResourceUri resourceUri, string newFullPath)
	{
		string baseLocationPath;
		// "\" is necessary at the end otherwise the path cannot be resolved correctly by ResourceUri.Uri
		switch (resourceUri.UriType)
		{
			case UriType.ApplicationRelative:
				baseLocationPath = $"{relativePathsNamespacePrefix};%APPLICATIONDIR%\\";
				break;
			case UriType.ProjectRelative:
				baseLocationPath = $"{relativePathsNamespacePrefix};%PROJECTDIR%\\";
				break;
			case UriType.USBRelative:
				baseLocationPath = $"%USB{resourceUri.USBNumber}%/";
				break;
			default:
				throw new CoreConfigurationException($"UriType '{resourceUri.UriType}' not expected");
		}

		// Extract the relative path from %APPLICATIONDIR%, %PROJECTDIR%, %USB<n>% by removing the computed baseLocationPath.
		// E.g. On Windows with 'D:\\MyFolder\\SubFolder' removing '%USB1%/'=='D:\\' results in 'MyFolder\\SubFolder'.
		// E.g. On Unix with '/storage/usb1/MyFolder/SubFolder' removing '%USB1%/'=='/storage/usb1' results in '/MyFolder/SubFolder'.
		var fullBaseLocationPath = new ResourceUri(baseLocationPath).Uri;
		var resultRelativePath = newFullPath.Substring(fullBaseLocationPath.Length);
		if (string.IsNullOrEmpty(resultRelativePath))
			return baseLocationPath;

		if (Environment.OSVersion.Platform != PlatformID.Unix)
		{
			// In case of %APPLICATIONDIR%, %PROJECTDIR% remove the initial "/".
			// Windows USB path starts with <Drive>:/ so in that case the initial character has not to be removed:
			// i.e. D:/MyFolder has "MyFolder" has resultedRelativePath, so nothing has to be removed
			if (resourceUri.UriType != UriType.USBRelative)
				resultRelativePath = resultRelativePath.Substring(1);
		}
		else
		{
			// On Unix the initial "/" must be removed always.
			// A Unix USB path has to be managed as a normal filesyestem path:
			// i.e. /storage/usb1/MyFolder has "/myFolder" as resultedRelativePath, so "/" has to be removed
			resultRelativePath = resultRelativePath.Substring(1);
		}

		return baseLocationPath + resultRelativePath;
	}

	private string AddNamespacePrefixIfNecessary(string incomingResourceUriString)
	{
		if (incomingResourceUriString.StartsWith("%APPLICATIONDIR") || incomingResourceUriString.StartsWith("%PROJECTDIR"))
			incomingResourceUriString = $"{relativePathsNamespacePrefix};{incomingResourceUriString}";

		return incomingResourceUriString;
	}

	private bool IsDirectory(string path)
	{
		return Directory.Exists(path);
	}

	private bool IsTopLevelFolder(ResourceUri currentResourceUri)
	{
		return currentResourceUri.ApplicationRelativePath == string.Empty ||
			currentResourceUri.ProjectRelativePath == string.Empty ||
			currentResourceUri.USBRelativePath == string.Empty;
	}

	private IUAVariable pathVariable;
	private IUAVariable fullPathVariable;
	private IUAVariable selectedItemVariable;

	private static string relativePathsNamespacePrefix;
}
