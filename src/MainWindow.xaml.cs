using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Ionic.Zip;
using LauncherConfig;

namespace MalyOtLauncherUpdate
{
	public partial class MainWindow : Window
	{
		static string launcerConfigUrl = "https://raw.githubusercontent.com/mayaradj/launcher-malyot/refs/heads/main/launcher_config.json";
		// Load informations of launcher_config.json file
		static ClientConfig clientConfig = ClientConfig.loadFromFile(launcerConfigUrl);

		static string clientExecutableName = clientConfig.clientExecutable;
		static string urlClient = clientConfig.newClientUrl;
		static string programVersion = clientConfig.launcherVersion;

		string newVersion = "";
		bool clientDownloaded = false;
		bool needUpdate = false;

		static readonly HttpClient httpClient = new HttpClient();
		WebClient webClient = new WebClient();

		private string GetLauncherPath(bool onlyBaseDirectory = false)
		{
			string launcherPath = "";
			if (string.IsNullOrEmpty(clientConfig.clientFolder) || onlyBaseDirectory) {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString();
			} else {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString() + "/" + clientConfig.clientFolder;
			}

			return launcherPath;
		}

		public MainWindow()
		{
			System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
			InitializeComponent();
		}

		static void CreateShortcut()
		{
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string shortcutPath = Path.Combine(desktopPath, clientConfig.clientFolder + ".lnk");
			Type t = Type.GetTypeFromProgID("WScript.Shell");
			dynamic shell = Activator.CreateInstance(t);
			var lnk = shell.CreateShortcut(shortcutPath);
			try
			{
				lnk.TargetPath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
				lnk.Description = clientConfig.clientFolder;
				lnk.Save();
			}
			finally
			{
				System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
			}
		}

		private void TibiaLauncher_Load(object sender, RoutedEventArgs e)
		{
			ImageLogoServer.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo.png"));
			ImageLogoCompany.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo_company.png"));

			// Sempre baixa o arquivo remoto para um arquivo temporário
			string tempConfigPath = Path.Combine(GetLauncherPath(true), "launcher_config_remote.json");
			try
			{
				new WebClient().DownloadFile(launcerConfigUrl, tempConfigPath);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Erro ao baixar o arquivo de configuração remoto: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// Lê as versões
			string remoteVersion = "";
			string localVersion = "";
			if (File.Exists(tempConfigPath))
			{
				try
				{
					using (StreamReader stream = new StreamReader(tempConfigPath))
					{
						dynamic jsonString = stream.ReadToEnd();
						dynamic versionclient = JsonConvert.DeserializeObject(jsonString);
						remoteVersion = versionclient["clientVersion"];
					}
				}
				catch { }
			}
			if (File.Exists(GetLauncherPath(true) + "/launcher_config.json"))
			{
				try
				{
					using (StreamReader stream = new StreamReader(GetLauncherPath(true) + "/launcher_config.json"))
					{
						dynamic jsonString = stream.ReadToEnd();
						dynamic versionclient = JsonConvert.DeserializeObject(jsonString);
						localVersion = versionclient["clientVersion"];
					}
				}
				catch { }
			}

			labelVersion.Text = "v" + programVersion;
			progressbarDownload.Visibility = Visibility.Collapsed;
			labelClientVersion.Visibility = Visibility.Collapsed;
			labelDownloadPercent.Visibility = Visibility.Collapsed;

			if (remoteVersion != localVersion)
			{
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
				buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_update.png"));
				labelClientVersion.Content = remoteVersion;
				labelClientVersion.Visibility = Visibility.Visible;
				buttonPlay.Visibility = Visibility.Visible;
				buttonPlay_tooltip.Text = "Update";
				needUpdate = true;
			}
			else if (!File.Exists(GetLauncherPath(true) + "/launcher_config.json") || (Directory.Exists(GetLauncherPath()) && Directory.GetFiles(GetLauncherPath()).Length == 0 && Directory.GetDirectories(GetLauncherPath()).Length == 0))
			{
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
				buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_update.png"));
				labelClientVersion.Content = "Download";
				labelClientVersion.Visibility = Visibility.Visible;
				buttonPlay.Visibility = Visibility.Visible;
				buttonPlay_tooltip.Text = "Download";
				needUpdate = true;
			}
			else
			{
				// Se as versões são iguais, mostra o botão de play normalmente
				buttonPlay.Visibility = Visibility.Visible;
				buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_play.png"));
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
				labelClientVersion.Content = localVersion;
				labelClientVersion.Visibility = Visibility.Visible;
				buttonPlay_tooltip.Text = localVersion;
			}
		}

		static string GetClientVersion(string path)
		{
			string json = path + "/launcher_config.json";
			StreamReader stream = new StreamReader(json);
			dynamic jsonString = stream.ReadToEnd();
			dynamic versionclient = JsonConvert.DeserializeObject(jsonString);
			foreach (string version in versionclient)
			{
				return version;
			}

			return "";
		}

		private void AddReadOnly()
		{
			// If the files "eventschedule/boostedcreature/onlinenumbers" exist, set them as read-only
			string eventSchedulePath = GetLauncherPath() + "/cache/eventschedule.json";
			if (File.Exists(eventSchedulePath)) {
				File.SetAttributes(eventSchedulePath, FileAttributes.ReadOnly);
			}
			string boostedCreaturePath = GetLauncherPath() + "/cache/boostedcreature.json";
			if (File.Exists(boostedCreaturePath)) {
				File.SetAttributes(boostedCreaturePath, FileAttributes.ReadOnly);
			}
			string onlineNumbersPath = GetLauncherPath() + "/cache/onlinenumbers.json";
			if (File.Exists(onlineNumbersPath)) {
				File.SetAttributes(onlineNumbersPath, FileAttributes.ReadOnly);
			}
		}

		private void UpdateClient()
		{
			if (!Directory.Exists(GetLauncherPath(true)))
			{
				Directory.CreateDirectory(GetLauncherPath());
			}
			labelDownloadPercent.Visibility = Visibility.Visible;
			progressbarDownload.Visibility = Visibility.Visible;
			labelClientVersion.Visibility = Visibility.Collapsed;
			buttonPlay.Visibility = Visibility.Collapsed;
			webClient.DownloadProgressChanged += Client_DownloadProgressChanged;
			webClient.DownloadFileCompleted += Client_DownloadFileCompleted;
			webClient.DownloadFileAsync(new Uri(urlClient), GetLauncherPath() + "/tibia.zip");
		}

		private void buttonPlay_Click(object sender, RoutedEventArgs e)
		{
			// Sempre baixa o arquivo remoto para um arquivo temporário antes de qualquer ação
			string tempConfigPath = Path.Combine(GetLauncherPath(true), "launcher_config_remote.json");
			try
			{
				new WebClient().DownloadFile(launcerConfigUrl, tempConfigPath);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Erro ao baixar o arquivo de configuração remoto: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// Lê as versões
			string remoteVersion = "";
			string localVersion = "";
			if (File.Exists(tempConfigPath))
			{
				try
				{
					using (StreamReader stream = new StreamReader(tempConfigPath))
					{
						dynamic jsonString = stream.ReadToEnd();
						dynamic versionclient = JsonConvert.DeserializeObject(jsonString);
						remoteVersion = versionclient["clientVersion"];
					}
				}
				catch { }
			}
			if (File.Exists(GetLauncherPath(true) + "/launcher_config.json"))
			{
				try
				{
					using (StreamReader stream = new StreamReader(GetLauncherPath(true) + "/launcher_config.json"))
					{
						dynamic jsonString = stream.ReadToEnd();
						dynamic versionclient = JsonConvert.DeserializeObject(jsonString);
						localVersion = versionclient["clientVersion"];
					}
				}
				catch { }
			}

			// Se as versões forem diferentes, força o update
			if (remoteVersion != localVersion)
			{
				UpdateClient();
				if (File.Exists(tempConfigPath))
				{
					File.Delete(tempConfigPath);
				}
				return;
			}

			// Se as versões forem iguais, permite abrir o cliente normalmente
			if (clientDownloaded == true || !Directory.Exists(GetLauncherPath(true)))
			{
				Process.Start(GetLauncherPath() + "/bin/" + clientExecutableName);
				this.Close();
			}
			else
			{
				try
				{
					UpdateClient();
				}
				catch (Exception ex)
				{
					labelVersion.Text = ex.ToString();
				}
			}
		}

		private void ExtractZip(string path, ExtractExistingFileAction existingFileAction)
		{
			try
			{
				using (ZipFile modZip = ZipFile.Read(path))
				{
					System.Diagnostics.Debug.WriteLine($"Arquivo ZIP aberto com {modZip.Count} entradas");
					
					foreach (ZipEntry zipEntry in modZip)
					{
						System.Diagnostics.Debug.WriteLine($"Extraindo: {zipEntry.FileName}");
						zipEntry.Extract(GetLauncherPath(), existingFileAction);
					}
				}
				System.Diagnostics.Debug.WriteLine("Descompactação concluída com sucesso");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Erro na descompactação: {ex.Message}");
				throw;
			}
		}

		private async void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
			buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_play.png"));

			// Verificar se o download foi bem-sucedido
			if (e.Error != null)
			{
				MessageBox.Show($"Erro no download: {e.Error.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			string zipPath = GetLauncherPath() + "/tibia.zip";
			
			// Verificar se o arquivo ZIP existe
			if (!File.Exists(zipPath))
			{
				MessageBox.Show("Arquivo ZIP não encontrado após download", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (clientConfig.replaceFolders)
			{
				foreach (ReplaceFolderName folderName in clientConfig.replaceFolderName)
				{
					string folderPath = Path.Combine(GetLauncherPath(), folderName.name);
					if (Directory.Exists(folderPath))
					{
						Directory.Delete(folderPath, true);
					}
				}
			}

			// Adds the task to a secondary task to prevent the program from crashing while this is running
			await Task.Run(() =>
			{
				try
				{
					Directory.CreateDirectory(GetLauncherPath());
					ExtractZip(zipPath, ExtractExistingFileAction.OverwriteSilently);
					File.Delete(zipPath);
				}
				catch (Exception ex)
				{
					// Log do erro para debug
					System.Diagnostics.Debug.WriteLine($"Erro na descompactação: {ex.Message}");
					throw;
				}
			});
			progressbarDownload.Value = 100;

			// Download launcher_config.json from url to the launcher path
			WebClient webClient = new WebClient();
			string localPath = Path.Combine(GetLauncherPath(true), "launcher_config.json");
			webClient.DownloadFile(launcerConfigUrl, localPath);

			// Após a atualização, substituir o arquivo launcher_config.json local pelo remoto e remover o temporário
			string tempConfigPath = Path.Combine(GetLauncherPath(true), "launcher_config_remote.json");
			if (File.Exists(tempConfigPath))
			{
				File.Copy(tempConfigPath, localPath, true);
				File.Delete(tempConfigPath); // Remove o arquivo temporário
			}

			AddReadOnly();
			CreateShortcut();

			needUpdate = false;
			clientDownloaded = true;
			labelClientVersion.Content = GetClientVersion(GetLauncherPath(true));
			buttonPlay_tooltip.Text = GetClientVersion(GetLauncherPath(true));
			labelClientVersion.Visibility = Visibility.Visible;
			buttonPlay.Visibility = Visibility.Visible;
			progressbarDownload.Visibility = Visibility.Collapsed;
			labelDownloadPercent.Visibility = Visibility.Collapsed;
		}

		private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			progressbarDownload.Value = e.ProgressPercentage;
			if (progressbarDownload.Value == 100) {
				labelDownloadPercent.Content = "Finishing, wait...";
			} else {
				labelDownloadPercent.Content = SizeSuffix(e.BytesReceived) + " / " + SizeSuffix(e.TotalBytesToReceive);
			}
		}

		static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
		static string SizeSuffix(Int64 value, int decimalPlaces = 1)
		{
			if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
			if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
			if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

			int mag = (int)Math.Log(value, 1024);
			decimal adjustedSize = (decimal)value / (1L << (mag * 10));

			if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
			{
				mag += 1;
				adjustedSize /= 1024;
			}
			return string.Format("{0:n" + decimalPlaces + "} {1}",
				adjustedSize,
				SizeSuffixes[mag]);
		}

		private void buttonPlay_MouseEnter(object sender, MouseEventArgs e)
		{
			if (File.Exists(GetLauncherPath() + "/launcher_config.json"))
			{
				string actualVersion = GetClientVersion(GetLauncherPath(true));
				if (newVersion != actualVersion)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_update.png")));
				}
				if (newVersion == actualVersion)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_play.png")));
				}
			}
			else
			{
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_update.png")));
			}
		}

		private void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
		{
			if (File.Exists(GetLauncherPath(true) + "/launcher_config.json"))
			{
				string actualVersion = GetClientVersion(GetLauncherPath(true));
				if (newVersion != actualVersion)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
				}
				if (newVersion == actualVersion)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
				}
			}
			else
			{
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void RestoreButton_Click(object sender, RoutedEventArgs e)
		{
			if (ResizeMode != ResizeMode.NoResize)
			{
				if (WindowState == WindowState.Normal)
					WindowState = WindowState.Maximized;
				else
					WindowState = WindowState.Normal;
			}
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

	}
}
