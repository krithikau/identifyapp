using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace IdentifyTest
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string subscriptionKey = #microsoft api subscription key
        private const string faceEndpoint = "https://eastus.api.cognitive.microsoft.com";
        
        //API connection to Azure
        private readonly IFaceClient faceClient = new FaceClient(
            new ApiKeyServiceClientCredentials(subscriptionKey),
            new System.Net.Http.DelegatingHandler[] { });
        
        private IList<DetectedFace> faceList;

        //API connection to Project Oxford
        static Microsoft.ProjectOxford.Face.FaceServiceClient faceServiceClient = 
            new Microsoft.ProjectOxford.Face.FaceServiceClient(subscriptionKey, "https://eastus.api.cognitive.microsoft.com/face/v1.0");

        private MediaCapture _mediaCapture;

        public MainPage()
        {
            this.InitializeComponent();

            Application.Current.Resuming += Application_Resuming;

            if (Uri.IsWellFormedUriString(faceEndpoint, UriKind.Absolute))
            {
                faceClient.Endpoint = faceEndpoint;
            }
                       
            InputName.Text = "";
            AddContactButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;                    
            
            //Makes start button invisible since it doesn't need to be run again
            StartButton.Visibility = Visibility.Collapsed;
        }
        
        // Pick a picture. Identifies and detects all faces in image. Writes details on side
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            faceDescriptionStatusBar.Text = "";
            var filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            filePicker.FileTypeFilter.Add(".jpg");
            filePicker.FileTypeFilter.Add(".jpeg");
            filePicker.FileTypeFilter.Add(".png");
            var file = await filePicker.PickSingleFileAsync();
            if (file == null || !file.IsAvailable) return;
            */
            // This is where we want to save to.
            var storageFolder = KnownFolders.SavedPictures;

            // Create the file that we're going to save the photo to.
            var file = await storageFolder.CreateFileAsync("sample.jpg", CreationCollisionOption.ReplaceExisting);

            // Update the file with the contents of the photograph.
            await _mediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);

            AppendMessage("Picture Taken");

            var property = await file.Properties.GetImagePropertiesAsync();
            var bitmap = BitmapFactory.New((int)property.Width, (int)property.Height);
            using (bitmap.GetBitmapContext())
            {
                using (var fileStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    bitmap = await BitmapFactory.New(1, 1).FromStream(fileStream, BitmapPixelFormat.Bgra8);
                }
            }
                        
            //Identify
            string personGroupId = "contacts";            
            var peopleNames = await IdentifyFace(personGroupId, file);
                                    
            //Detects faces. Loops thru faces to draw box and write identity and face attributes
            using (var imageStream = await file.OpenStreamForReadAsync())
            {
                faceList = await DetectFaces(imageStream);

                if (faceList == null)
                {
                    AppendMessage("null");
                    return;
                }
                if(faceList.Count == 0)
                {
                    faceDescriptionStatusBar.Text = "";
                    AppendMessage("No faces detected");
                    return;
                }

                faceDescriptionStatusBar.Text = "";
                AppendMessage($"{faceList.Count} Face(s) Detected!");
                AppendMessage(" ");

                if (faceList.Count > 10)
                {
                    AppendMessage("Too many people in picture to identify. Pick picture with at most 10 people.");
                    AppendMessage(" ");
                }

                for (int i = 0; i < faceList.Count; i++)
                {
                    string colorName;
                    var color = SetColor(i, out colorName);
                    DetectedFace face = faceList[i];
                                        
                    DrawRectangle(bitmap, face, color, 10);

                    // Add the emotions. Display all emotions over 50%.
                    StringBuilder sb = new StringBuilder();
                    Emotion emotionScores = face.FaceAttributes.Emotion;

                    if (emotionScores.Anger >= 0.5f) sb.Append(
                        String.Format("anger {0:F1}%, ", emotionScores.Anger * 100));
                    else if (emotionScores.Contempt >= 0.5f) sb.Append(
                        String.Format("contempt {0:F1}%, ", emotionScores.Contempt * 100));
                    else if (emotionScores.Disgust >= 0.5f) sb.Append(
                        String.Format("disgust {0:F1}%, ", emotionScores.Disgust * 100));
                    else if (emotionScores.Fear >= 0.5f) sb.Append(
                        String.Format("fear {0:F1}%, ", emotionScores.Fear * 100));
                    else if (emotionScores.Happiness >= 0.5f) sb.Append(
                        String.Format("happiness {0:F1}%, ", emotionScores.Happiness * 100));
                    else if (emotionScores.Neutral >= 0.5f) sb.Append(
                        String.Format("neutral {0:F1}%, ", emotionScores.Neutral * 100));
                    else if (emotionScores.Sadness >= 0.5f) sb.Append(
                        String.Format("sadness {0:F1}%, ", emotionScores.Sadness * 100));
                    else if (emotionScores.Surprise >= 0.5f) sb.Append(
                        String.Format("surprise {0:F1}%, ", emotionScores.Surprise * 100));
                    else sb.Append(
                        "emotion cannot be detected");

                    //Write details on side
                    if (faceList.Count > 10)
                    {
                        AppendMessage("Person" + i + " (" + colorName + ")");
                        AppendMessage($"Gender: {face.FaceAttributes.Gender}");
                        AppendMessage("Emotion: " + sb);
                    }
                    else if (peopleNames.Count != faceList.Count)
                    {
                        AppendMessage("Person not in your contacts" + " (" + colorName + ")");
                        AppendMessage($"Gender: {face.FaceAttributes.Gender}");
                        AppendMessage("Emotion: " + sb);
                    }
                    else
                    {
                        AppendMessage(peopleNames[i] + " (" + colorName + ")");
                        AppendMessage($"Gender: {face.FaceAttributes.Gender}");
                        AppendMessage("Emotion: " + sb);
                    }                                     
                    
                    AppendMessage(" ");
                }
            }
            //Prints out image with boxes around faces
            imagePhoto.Source = bitmap;
        }

        // creates person group; does not need to be run ever again
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            string personGroupId = "contacts";
            string personGroupName = "My Contacts";
                        
            String created;
            try
            {
                created = await CreatePersonGroup(personGroupId, personGroupName);
            }
            catch
            {
                await faceServiceClient.DeletePersonGroupAsync(personGroupId);
                created = await CreatePersonGroup(personGroupId, personGroupName);
            }
            AppendMessage(created);

        }

        // takes input text from textbox as contact name. Adds person to person group with single picture and retrains
        private async void AddContactButton_Click(object sender, RoutedEventArgs e)
        {
            faceDescriptionStatusBar.Text = "";
            imagePhoto.Source = null;

            string personGroupId = "contacts";
            string contactName = InputName.Text;
                         
            if(contactName == "")
            {
                AppendMessage("Contact Name needs to be entered in textbox");
                return;
            }

            var people = await faceServiceClient.ListPersonsAsync(personGroupId);
            int count = people.Count();

            for(int i = 0; i<count; i++)
            {                
                String name1 = people[i].Name;
                if (name1 == contactName)
                {
                    AppendMessage("Contact with that name already exists");
                    return;
                }
            }
                        

            AppendMessage("Upload image of contact");
            Boolean added = await AddPersonToGroup(personGroupId, contactName);
            
            if (added == true )
            {
                AppendMessage("added " + contactName);
                Boolean trained = await TrainingAI(personGroupId);

                if (trained == true)
                {
                    AppendMessage("trained = " + trained.ToString());
                }
                else
                {
                    AppendMessage("trained = " + trained.ToString());
                }

            }
            else
            {
                AppendMessage("added = false");
                AppendMessage("Could not detect face. Try adding contact with a different picture");
            }
            InputName.Text = "";

        }

        // takes input text from textbox as contact name. Delets person from person group and retrains
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            faceDescriptionStatusBar.Text = "";
            imagePhoto.Source = null;

            Boolean deleted;
            string personGroupId = "contacts";
            string contactName = InputName.Text;

            if (contactName == "")
            {
                AppendMessage("Contact Name is empty");
                return;
            }

            Boolean exists = false;
            var people = await faceServiceClient.ListPersonsAsync(personGroupId);
            int count = people.Count();
            int i;

            for (i = 0; i < count; i++)
            {
                String name1 = people[i].Name;
                if (name1 == contactName)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
            {
                Guid personId = people[i].PersonId;
                deleted = await DeletePersonFromGroup(personGroupId, personId);

                AppendMessage("deleted " + contactName);

                Boolean trained = await TrainingAI(personGroupId);

                if (trained == true)
                {
                    AppendMessage("trained = " + trained.ToString());

                }
                else
                {
                    AppendMessage("trained = " + trained.ToString());
                }


                InputName.Text = "";
            }
            else
            {
                AppendMessage("Person does not exist");
            }

        }

        private async void InputName_TextChanging(object sender, TextBoxTextChangingEventArgs args)
        {
            string contactName = InputName.Text;
            Boolean notEmpty;
            if (contactName != "")
            {
                notEmpty = true;
            }
            else
            {
                notEmpty = false;
            }
            AddContactButton.IsEnabled = notEmpty;
            DeleteButton.IsEnabled = notEmpty;                
        }

        public static async Task<String> CreatePersonGroup(string personGroupId, string personGroupName)
        {
            String created;
            try
            {
                await faceServiceClient.CreatePersonGroupAsync(personGroupId, personGroupName);                
                created = "Create Person Group succeed";
            }
            catch (Exception ex)
            {                
                created = "Error " + ex.Message;                              
            }
            return created;
        }

        public async Task<Boolean> AddPersonToGroup(string personGroupId, string personName)
        {
            Boolean added = false;
            try
            {
                await faceServiceClient.GetPersonGroupAsync(personGroupId);
                Microsoft.ProjectOxford.Face.Contract.CreatePersonResult personResult = await faceServiceClient.CreatePersonAsync(personGroupId, personName);
                                
                Boolean detect = await DetectFaceAndRegister(personGroupId, personResult);
                added = true;
            }
            catch (Exception ex)
            {                                
            }
            return added;
        }

        public static async Task<Boolean> DeletePersonFromGroup(string personGroupId, Guid personId)
        {
            Boolean deleted = false;
            try
            {                
                await faceServiceClient.DeletePersonAsync(personGroupId, personId);                
                deleted = true;

            }
            catch (Exception ex)
            {
            }
            return deleted;
        }

        private async Task<Boolean> DetectFaceAndRegister(string personGroupId, Microsoft.ProjectOxford.Face.Contract.CreatePersonResult personResult)
        {
            Boolean done;
            /*
            var filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            filePicker.FileTypeFilter.Add(".jpg");
            filePicker.FileTypeFilter.Add(".jpeg");
            filePicker.FileTypeFilter.Add(".png");
            var file = await filePicker.PickSingleFileAsync();
            if (file == null || !file.IsAvailable) return false;
            */
            // This is where we want to save to.
            var storageFolder = KnownFolders.SavedPictures;

            // Create the file that we're going to save the photo to.
            var file = await storageFolder.CreateFileAsync("contact.jpg", CreationCollisionOption.ReplaceExisting);

            // Update the file with the contents of the photograph.
            await _mediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);

            using (var s = await file.OpenStreamForReadAsync())
            {
                await faceServiceClient.AddPersonFaceAsync(personGroupId, personResult.PersonId, s);
            }
                        
            done = true;
            return done;
        }

        private static async Task<Boolean> TrainingAI(string personGroupId)
        {
            Boolean trained = false;
            await faceServiceClient.TrainPersonGroupAsync(personGroupId);
            Microsoft.ProjectOxford.Face.Contract.TrainingStatus training = null;
            while (true)
            {
                training = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);
                if(training.Status != Microsoft.ProjectOxford.Face.Contract.Status.Running)
                {                    
                    break;
                }                
                await Task.Delay(1000);
            }            
            trained = true;
            return trained;
        }
              
        private static async Task<IList<string>> IdentifyFace(string personGroupId, StorageFile file)
        {
            
            List<string> people = new List<string>();

            using (var s = await file.OpenStreamForReadAsync())
            {
                var faces = await faceServiceClient.DetectAsync(s);
                int faceNumber = faces.Count();
                var faceIds = faces.Select(face => face.FaceId).ToArray();
                int faceIdsNumber = faceIds.Count();
                                
                try
                {
                    var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds, faceIdsNumber);
                    foreach (var identifyResult in results)
                    {                        
                        if (identifyResult.Candidates.Length == 0)
                        {                            
                            people.Add("Person not in your contact list");
                        }                                                        
                        else
                        {                                                     
                            var candidateId = identifyResult.Candidates[0].PersonId;
                            var person = await faceServiceClient.GetPersonAsync(personGroupId, candidateId);

                            people.Add(person.Name);                                                                                                          
                        }
                    }
                }
                catch (Exception ex)
                {
                    people.Add("Error " + ex.Message);                    
                }                
            }
            return people;
        }               

        private async Task<IList<DetectedFace>> DetectFaces(Stream imageStream)
        {
                var attributes = new List<FaceAttributeType>();
                attributes.Add(FaceAttributeType.Age);
                attributes.Add(FaceAttributeType.Gender);
                attributes.Add(FaceAttributeType.Emotion);

                try
                {
                    using (imageStream)
                    {
                        IList<DetectedFace> faceList =
                            await faceClient.Face.DetectWithStreamAsync(
                                imageStream, true, false, attributes);
                        return faceList;
                    }
                }
                catch (APIErrorException f)
                {
                    MessageBox.Text = f.Message;
                    return new List<DetectedFace>();
                }
                catch (Exception e)
                {
                    MessageBox.Text = "Error: " + e.Message;
                    return new List<DetectedFace>();
                }
        }
       
        private void AppendMessage(string message)
        {
                faceDescriptionStatusBar.Text = faceDescriptionStatusBar.Text + "\r\n" + message;                
        }

        private static Color SetColor(int faceCount, out string colorName)
        {
                Color color;
                switch (faceCount)
                {
                    case 1: color = Colors.Red; colorName = "Red"; break;
                    case 2: color = Colors.Blue; colorName = "Blue"; break;
                    case 3: color = Colors.Green; colorName = "Green"; break;
                    case 4: color = Colors.Yellow; colorName = "Yellow"; break;
                    case 5: color = Colors.Purple; colorName = "Purple"; break;
                    case 6: color = Colors.Black; colorName = "Black"; break;
                    case 7: color = Colors.Maroon; colorName = "Maroon"; break;
                    case 8: color = Colors.White; colorName = "White"; break;
                    case 9: color = Colors.Navy; colorName = "Navy"; break;
                    default: color = Colors.Orange; colorName = "Orange"; break;
                }
                return color;
        }

        private static void DrawRectangle(WriteableBitmap bitmap, DetectedFace face, Color color, int thinkness)
        {
                var left = face.FaceRectangle.Left;
                var top = face.FaceRectangle.Top;
                var width = face.FaceRectangle.Width;
                var height = face.FaceRectangle.Height;
            
                DrawRectangle(bitmap, left, top, width, height, color, thinkness);
        }

        private static void DrawRectangle(WriteableBitmap bitmap, int left, int top, int width, int height, Color color, int thinkness)
        {
                var x1 = left;
                var y1 = top;
                var x2 = left + width;
                var y2 = top + height;

                bitmap.DrawRectangle(x1, y1, x2, y2, color);

                for (var i = 0; i < thinkness; i++)
                {
                    bitmap.DrawRectangle(x1--, y1--, x2++, y2++, color);
                }
        }

        private async void Application_Resuming(object sender, object o)
        {
            await InitializeCameraAsync();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitializeCameraAsync();
        }

        private async Task InitializeCameraAsync()
        {
            if (_mediaCapture == null)
            {
                // Get the camera devices
                var cameraDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                // try to get the back facing device for a phone
                var backFacingDevice = cameraDevices
                    .FirstOrDefault(c => c.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Back);

                // but if that doesn't exist, take the first camera device available
                var preferredDevice = backFacingDevice ?? cameraDevices.FirstOrDefault();

                // Create MediaCapture
                _mediaCapture = new MediaCapture();

                // Initialize MediaCapture and settings
                await _mediaCapture.InitializeAsync(
                    new MediaCaptureInitializationSettings
                    {
                        VideoDeviceId = preferredDevice.Id
                    });

                // Set the preview source for the CaptureElement
                PreviewControl.Source = _mediaCapture;

                // Start viewing through the CaptureElement 
                await _mediaCapture.StartPreviewAsync();
            }
        }

    }        
}
