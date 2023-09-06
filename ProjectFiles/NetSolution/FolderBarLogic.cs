#region Using directives
using System;
using UAManagedCore;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.Core;
#endregion

public class FolderBarLogic : BaseNetLogic
{
	public override void Start()
	{
		relativePathsNamespacePrefix = $"ns={LogicObject.NodeId.NamespaceIndex}";

		// Path variables
		pathVariable = LogicObject.GetVariable("Path");
		if (pathVariable == null)
			throw new CoreConfigurationException("Path variable not found in FilesystemBrowserLogic");

		// FolderBar variables
		locationsComboBox = (ComboBox)Owner.GetObject("Locations");
		if (locationsComboBox == null)
			throw new CoreConfigurationException("Locations combo box not found");

		relativePathTextBox = (TextBox)Owner.GetObject("RelativePath");
		if (relativePathTextBox == null)
			throw new CoreConfigurationException("RelativePath textbox not found");

		// Fix Path variable non-legal values, i.e. non-existing USB or invalid path (not starting with %PROJECTDIR%\ or %APPLICATIONDIR%\)
		var startFolderPathResourceUri = FixPathVariableIfNecessary();

		InitalizeLocationsObject();
		InitializeComboBoxAndTextBox(startFolderPathResourceUri);

		pathVariable.VariableChange += PathVariable_VariableChange;
		locationsComboBox.SelectedValueVariable.VariableChange += SelectedValueComboBox_VariableChange;
		relativePathTextBox.OnUserTextChanged += RelativePathTextBox_UserTextChanged;
	}

	public override void Stop()
	{
		pathVariable.VariableChange -= PathVariable_VariableChange;
		locationsComboBox.SelectedValueVariable.VariableChange -= SelectedValueComboBox_VariableChange;
		relativePathTextBox.OnUserTextChanged -= RelativePathTextBox_UserTextChanged;
	}

	private void SelectedValueComboBox_VariableChange(object sender, VariableChangeEventArgs e)
	{
		// Clear RelativePath Textbox and update the current path (a new browse is made)
		relativePathTextBox.Text = string.Empty;
		pathVariable.Value = new ResourceUri(e.NewValue);
	}

	private void RelativePathTextBox_UserTextChanged(object sender, UserTextChangedEvent e)
	{
		var pathVariableResourceUri = new ResourceUri(pathVariable.Value);
		var updatedRelativePathString = e.NewText.Text;

		// Determine the base path (from the previous value)
		string baseUriTypePath;
		switch (pathVariableResourceUri.UriType)
		{
			case UriType.ApplicationRelative:
				baseUriTypePath = $"{relativePathsNamespacePrefix};{applicationDirString}";
				break;
			case UriType.ProjectRelative:
				baseUriTypePath = $"{relativePathsNamespacePrefix};{projectDirString}";
				break;
			case UriType.USBRelative:
				baseUriTypePath = $"%USB{pathVariableResourceUri.USBNumber}%/";
				break;
			default:
				throw new CoreConfigurationException($"ResourceUri of type {pathVariableResourceUri.UriType} is not supported");
		}

		// Update pathVariable value with the text inserted into the textbox
		pathVariable.Value = baseUriTypePath + updatedRelativePathString;
	}

	private void PathVariable_VariableChange(object sender, VariableChangeEventArgs e)
	{
		var updatedPathResourceUri = new ResourceUri(e.NewValue);

		// Get the relative path and set it into the textbox
		try
		{
			var relativeFolderPath = "";
			switch (updatedPathResourceUri.UriType)
			{
				case UriType.ApplicationRelative:
					relativeFolderPath = updatedPathResourceUri.ApplicationRelativePath;
					break;
				case UriType.ProjectRelative:
					relativeFolderPath = updatedPathResourceUri.ProjectRelativePath;
					break;
				case UriType.USBRelative:
					relativeFolderPath = updatedPathResourceUri.USBRelativePath;
					break;
				default:
					throw new CoreConfigurationException("Path variable must start with: '%PROJECTDIR%\\', '%APPLICATIONDIR%\\' or '%USB<n>%'");
			}

			relativePathTextBox.Text = relativeFolderPath;
		}
		catch (Exception exception)
		{
			Log.Error("FolderBarLogic", $"Path '{e.NewValue.Value}' not found: {exception.Message}");
			return;
		}
	}

	private ResourceUri FixPathVariableIfNecessary()
	{
		var startFolderPathResourceUri = new ResourceUri(pathVariable.Value);

		// Check that PathVariable has a legal value, i.e. it is a value that:
		// - is a relative path
		// - in case of USB devices it represents a connected USB
		// In case of wrong value, the %PROJECTDIR% folder is taken as the default value
		var defaultResourceUri = new ResourceUri($"{relativePathsNamespacePrefix};{projectDirString}");
		if (!IsRelativeResourceUri(startFolderPathResourceUri))
		{
			pathVariable.Value = defaultResourceUri;
			return defaultResourceUri;
		}

		// Check that the start folder resource uri can be resolved (i.e. non existing USB device)
		try
		{
			var uri = startFolderPathResourceUri.Uri;
		}
		catch (Exception)
		{
			pathVariable.Value = defaultResourceUri;
			return defaultResourceUri;
		}

		return startFolderPathResourceUri;
	}

	private void InitalizeLocationsObject()
	{
		var locationsObject = Owner.Owner.GetObject("Locations");
		if (locationsObject == null)
			throw new CoreConfigurationException("Locations object not found");

		// Set the namespace prefix to %APPLICATIONDIR% and %PROJECTDIR%
		foreach (IUAVariable location in locationsObject.Children)
			location.Value = $"{relativePathsNamespacePrefix};{location.Value.Value}";

		// Detect connected USB devices
		for (uint i = 1; i <= maxConnectedUsbDevices; ++i)
		{
			var usbResourceUriString = $"%USB{i}%";
			string usbResourceUri;
			try
			{
				usbResourceUri = new ResourceUri(usbResourceUriString).Uri;
			}
			catch (CoreException)
			{
				return;
			}

			var usbResourceUriVariable = InformationModel.MakeVariable($"USB{i}", FTOptix.Core.DataTypes.ResourceUri);
			usbResourceUriVariable.Value = new ResourceUri(usbResourceUriString);

			var localeIds = Session.User.LocaleId;
			if (String.IsNullOrEmpty(localeIds))
				Log.Error("FolderBarLogic", "No locales found for the current user");

			// The display name for USB devices has no need for translations in different locales
			//foreach (var localeId in localeIds)
				usbResourceUriVariable.DisplayName = new LocalizedText("ComboBoxFileSelectorUSBDisplayName", $"USB {i}", localeIds);

			locationsObject.Add(usbResourceUriVariable);
		}
	}

	private void InitializeComboBoxAndTextBox(ResourceUri startFolderPathResourceUri)
	{
		var baseLocationPath = "";
		var relativeFolderPath = "";

		// Determine the starting location and its relative path
		switch (startFolderPathResourceUri.UriType)
		{
			case UriType.ApplicationRelative:
				baseLocationPath = $"{relativePathsNamespacePrefix};{applicationDirString}";
				relativeFolderPath = startFolderPathResourceUri.ApplicationRelativePath;
				break;
			case UriType.ProjectRelative:
				baseLocationPath = $"{relativePathsNamespacePrefix};{projectDirString}";
				relativeFolderPath = startFolderPathResourceUri.ProjectRelativePath;
				break;
			case UriType.USBRelative:
				baseLocationPath = $"%USB{startFolderPathResourceUri.USBNumber}%";
				relativeFolderPath = startFolderPathResourceUri.USBRelativePath;
				break;
		}

		// Initialize the textbox with the initial path
		relativePathTextBox.Text = relativeFolderPath;

		// Initalize the combo box with the starting location
		locationsComboBox.SelectedValue = baseLocationPath;
	}

	private bool IsRelativeResourceUri(ResourceUri resourceUri)
	{
		return resourceUri.UriType == UriType.ApplicationRelative ||
			resourceUri.UriType == UriType.ProjectRelative ||
			resourceUri.UriType == UriType.USBRelative;
	}

	private IUAVariable pathVariable;

	// FolderBar variables
	private TextBox relativePathTextBox;
	private ComboBox locationsComboBox;

	private const string applicationDirString = "%APPLICATIONDIR%\\";
	private const string projectDirString = "%PROJECTDIR%\\";
	private const uint maxConnectedUsbDevices = 5;

	private static string relativePathsNamespacePrefix;
}
