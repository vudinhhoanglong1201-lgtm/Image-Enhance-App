using System.Net.Http.Headers;

namespace Image_Enhance_App;

public partial class MainPage : ContentPage
{
    private List<FileResult> _selectedFiles = new();
    private List<byte[]> _enhancedImagesBytes = new();
    private readonly HttpClient _httpClient = new HttpClient();

    public MainPage()
    {
        InitializeComponent();
    }

    // 1. CHỌN NHIỀU ẢNH CÙNG LÚC
    private async void OnSelectImageClicked(object sender, EventArgs e)
    {
        try
        {
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Chọn các ảnh cần làm nét",
                FileTypes = FilePickerFileType.Images
            });

            if (results != null && results.Any())
            {
                _selectedFiles = results.ToList();
                _enhancedImagesBytes.Clear();

                var firstStream = await _selectedFiles[0].OpenReadAsync();
                ImgOriginal.Source = ImageSource.FromStream(() => firstStream);

                LblStatus.Text = $"Đã chọn {_selectedFiles.Count} ảnh!";
                BtnSave.IsEnabled = false;
                ImgEnhanced.Source = null;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể chọn ảnh: {ex.Message}", "OK");
        }
    }

    // 2. XỬ LÝ AI LẦN LƯỢT TỪNG ẢNH
    private async void OnEnhanceImageClicked(object sender, EventArgs e)
    {
        if (!_selectedFiles.Any())
        {
            await DisplayAlert("Thông báo", "Vui lòng chọn ít nhất 1 ảnh trước!", "OK");
            return;
        }

        BtnEnhance.IsEnabled = false;
        LoadingSpinner.IsVisible = true;
        LoadingSpinner.IsRunning = true;
        _enhancedImagesBytes.Clear();

        string apiUrl = "http://10.0.2.2:5123/enhance";
        int totalFiles = _selectedFiles.Count;
        int successCount = 0;

        try
        {
            for (int i = 0; i < totalFiles; i++)
            {
                var file = _selectedFiles[i];
                LblStatus.Text = $"Đang xử lý {i + 1}/{totalFiles}: {file.FileName}...";

                using var stream = await file.OpenReadAsync();
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(stream);

                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType ?? "image/jpeg");
                content.Add(streamContent, "file", file.FileName);

                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    byte[] resultBytes = await response.Content.ReadAsByteArrayAsync();
                    _enhancedImagesBytes.Add(resultBytes);
                    successCount++;

                    ImgEnhanced.Source = ImageSource.FromStream(() => new MemoryStream(resultBytes));
                }
            }

            if (successCount > 0)
            {
                LblStatus.Text = $"Phục hồi thành công {successCount}/{totalFiles} ảnh!";
                BtnSave.IsEnabled = true;
            }
            else
            {
                LblStatus.Text = "Xử lý thất bại!";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi Kết Nối", ex.Message, "OK");
            LblStatus.Text = "Xử lý thất bại!";
        }
        finally
        {
            BtnEnhance.IsEnabled = true;
            LoadingSpinner.IsVisible = false;
            LoadingSpinner.IsRunning = false;
        }
    }

    // 3. LƯU TỰ ĐỘNG VÀO BỘ SƯU TẬP ANDROID
    private async void OnSaveImageClicked(object sender, EventArgs e)
    {
        if (!_enhancedImagesBytes.Any())
        {
            await DisplayAlert("Thông báo", "Chưa có ảnh kết quả để lưu!", "OK");
            return;
        }

        try
        {
#if ANDROID
            // Lấy đường dẫn thư mục Pictures chuẩn trên Android
            string picturesPath = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryPictures)?.AbsolutePath
                ?? FileSystem.Current.AppDataDirectory;

            string appDir = Path.Combine(picturesPath, "ImageEnhanceApp");
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }

            List<string> savedFilePaths = new();

            for (int i = 0; i < _enhancedImagesBytes.Count; i++)
            {
                string fileName = $"Enhanced_{DateTime.Now:yyyyMMdd_HHmmss}_{i + 1}.jpg";
                string filePath = Path.Combine(appDir, fileName);

                await File.WriteAllBytesAsync(filePath, _enhancedImagesBytes[i]);
                savedFilePaths.Add(filePath);
            }

            // Gọi MediaScanner để ảnh xuất hiện lập tức trong Bộ sưu tập (Gallery)
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            Android.Media.MediaScannerConnection.ScanFile(
                context,
                savedFilePaths.ToArray(),
                new[] { "image/jpeg" },
                null
            );

            await DisplayAlert("Thành công", $"Đã lưu {_enhancedImagesBytes.Count} ảnh vào Album 'ImageEnhanceApp'!", "OK");
#else
            await DisplayAlert("Thông báo", "Tính năng này chỉ hỗ trợ trên thiết bị Android.", "OK");
#endif
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi Lưu File", ex.Message, "OK");
        }
    }
}