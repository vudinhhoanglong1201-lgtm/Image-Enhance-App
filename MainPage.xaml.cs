using System.Net.Http.Headers;
using CommunityToolkit.Maui.Storage;

namespace Image_Enhance_App;

public partial class MainPage : ContentPage
{
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
                _enhancedBytes = null;
                ImgEnhanced.Source = null;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể chọn ảnh: {ex.Message}", "OK");
        }
    }

    private async void OnEnhanceImageClicked(object sender, EventArgs e)
    {
        if (_selectedFile == null)
        {
            await DisplayAlert("Thông báo", "Vui lòng chọn ảnh trước!", "OK");
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

            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(_selectedFile.ContentType ?? "image/jpeg");
            content.Add(streamContent, "file", _selectedFile.FileName);

            string apiUrl = "http://10.0.2.2:5123/enhance";
            var response = await _httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _enhancedBytes = await response.Content.ReadAsByteArrayAsync();
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

    private async void OnSaveImageClicked(object sender, EventArgs e)
    {
        if (_enhancedBytes == null || _enhancedBytes.Length == 0)
        {
            await DisplayAlert("Thông báo", "Chưa có ảnh kết quả để lưu!", "OK");
            return;
        }

        try
        {
            using var stream = new MemoryStream(_enhancedBytes);
            string fileName = $"Enhanced_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";

            // Mở hộp thoại hệ thống cho phép chọn vị trí lưu file
            var fileSaverResult = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);

            if (fileSaverResult.IsSuccessful)
            {
                await DisplayAlert("Thành công", $"Đã lưu ảnh tại:\n{fileSaverResult.FilePath}", "OK");
            }
            else if (fileSaverResult.Exception != null)
            {
                await DisplayAlert("Lỗi Lưu File", fileSaverResult.Exception.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi Lưu File", ex.Message, "OK");
        }
    }
}