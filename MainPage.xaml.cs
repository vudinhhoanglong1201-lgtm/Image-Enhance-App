using System.Net.Http.Headers;

namespace Image_Enhance_App;

public partial class MainPage : ContentPage
{
    // Khai báo các biến lưu trữ toàn cục ở đây
    private FileResult? _selectedFile;
    private byte[]? _enhancedBytes;
    private readonly HttpClient _httpClient = new HttpClient();

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnSelectImageClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Chọn ảnh cần làm nét",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                _selectedFile = result;
                var stream = await result.OpenReadAsync();
                ImgOriginal.Source = ImageSource.FromStream(() => stream);
                LblStatus.Text = "Đã chọn ảnh thành công!";
                BtnSave.IsEnabled = false;
                _enhancedBytes = null; // Dòng 32 hết lỗi
                ImgEnhanced.Source = null;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", $"Không thể chọn ảnh: {ex.Message}", "OK");
        }
    }

    private async void OnEnhanceImageClicked(object sender, EventArgs e)
    {
        if (_selectedFile == null)
        {
            await DisplayAlertAsync("Thông báo", "Vui lòng chọn ảnh trước!", "OK");
            return;
        }

        BtnEnhance.IsEnabled = false;
        LoadingSpinner.IsVisible = true;
        LoadingSpinner.IsRunning = true;
        LblStatus.Text = "Đang gửi ảnh lên Server xử lý AI...";

        try
        {
            using var stream = await _selectedFile.OpenReadAsync();
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(stream);

            // Sửa dòng 61: Dùng MediaTypeHeaderValue.Parse
            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(_selectedFile.ContentType ?? "image/jpeg");
            content.Add(streamContent, "file", _selectedFile.FileName);

            string apiUrl = "http://10.0.2.2:5123/enhance";
            var response = await _httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _enhancedBytes = await response.Content.ReadAsByteArrayAsync(); // Dòng 72 hết lỗi
                ImgEnhanced.Source = ImageSource.FromStream(() => new MemoryStream(_enhancedBytes));
                LblStatus.Text = "Phục hồi thành công!";
                BtnSave.IsEnabled = true;
            }
            else
            {
                string errorDetail = await response.Content.ReadAsStringAsync();
                LblStatus.Text = $"Lỗi Server ({(int)response.StatusCode}): {errorDetail}";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi Kết Nối", ex.Message, "OK");
            LblStatus.Text = "Xử lý thất bại!";
        }
        finally
        {
            BtnEnhance.IsEnabled = true;
            LoadingSpinner.IsVisible = false;
            LoadingSpinner.IsRunning = false;
        }
    }

    private async void OnSaveImageClicked(object sender, EventArgs e)
    {
        if (_enhancedBytes == null || _enhancedBytes.Length == 0) return;

        try
        {
            string fileName = $"Enhanced_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            string targetPath = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);

            await File.WriteAllBytesAsync(targetPath, _enhancedBytes);
            await DisplayAlertAsync("Thành công", $"Đã lưu ảnh vào thiết bị:\n{targetPath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi Lưu File", ex.Message, "OK");
        }
    }
}