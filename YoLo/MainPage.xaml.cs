using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
// code based on this tutorial https://docs.microsoft.com/en-us/azure/cognitive-services/Face/Tutorials/FaceAPIinCSharpTutorial

namespace FaceMatch
{
    static class MediaElementExtensions
    {
        public static async Task PlayStreamAsync(
          this MediaElement mediaElement,
          IRandomAccessStream stream,
          bool disposeStream = true)
        {
            // bool is irrelevant here, just using this to flag task completion.
            TaskCompletionSource<bool> taskCompleted = new TaskCompletionSource<bool>();

            // Note that the MediaElement needs to be in the UI tree for events
            // like MediaEnded to fire.
            RoutedEventHandler endOfPlayHandler = (s, e) =>
            {
                if (disposeStream)
                {
                    stream.Dispose();
                }
                taskCompleted.SetResult(true);
            };
            mediaElement.MediaEnded += endOfPlayHandler;

            mediaElement.SetSource(stream, string.Empty);
            mediaElement.Play();

            await taskCompleted.Task;
            mediaElement.MediaEnded -= endOfPlayHandler;
        }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private uint _canvasActualWidth;
        private uint _canvasActualHeight;
        private MediaElement uiMediaElement = new MediaElement();
        private VideoFrame _currentVideoFrame;
        private SoftwareBitmapSource _softwareBitmapSource;
        private SoftwareBitmap _softwareBitmap;

        private const string subscriptionKey = ""; //put your cognitive services face api key in here
        private const string baseUri =
            "https://westus.api.cognitive.microsoft.com/face/v1.0";

        private readonly IFaceClient faceClient = new FaceClient(
            new ApiKeyServiceClientCredentials(subscriptionKey),
            new System.Net.Http.DelegatingHandler[] { });

        IList<DetectedFace> faceList;   // The list of detected faces.

        public MainPage()
        {
            this.InitializeComponent();

            if (Uri.IsWellFormedUriString(baseUri, UriKind.Absolute))
            {
                faceClient.BaseUri = new Uri(baseUri);
            }
        }

        async Task<IRandomAccessStream> SynthesizeTextToSpeechAsync(string text)
        {
            // Windows.Storage.Streams.IRandomAccessStream
            IRandomAccessStream stream = null;

            // Windows.Media.SpeechSynthesis.SpeechSynthesizer
            using (SpeechSynthesizer synthesizer = new SpeechSynthesizer())
            {
                // Windows.Media.SpeechSynthesis.SpeechSynthesisStream
                stream = await synthesizer.SynthesizeTextToStreamAsync(text);
            }

            return (stream);
        }

        async Task SpeakTextAsync(string text, MediaElement mediaElement)
        {
            IRandomAccessStream stream = await this.SynthesizeTextToSpeechAsync(text);

            await mediaElement.PlayStreamAsync(stream, true);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _softwareBitmapSource = new SoftwareBitmapSource();
            CurrentFrameImage.Source = _softwareBitmapSource;

            GetCameraSize();
            Window.Current.SizeChanged += Current_SizeChanged;

            await CameraPreview.StartAsync();
            CameraPreview.CameraHelper.FrameArrived += CameraHelper_FrameArrived;
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            GetCameraSize();
        }

        private void GetCameraSize()
        {
            _canvasActualWidth = (uint)CameraPreview.ActualWidth;
            _canvasActualHeight = (uint)CameraPreview.ActualHeight;
        }

        async void Click_Click(object sender, RoutedEventArgs e)
        {
            _softwareBitmap = _currentVideoFrame?.SoftwareBitmap;

            if (_softwareBitmap != null)
            {
                if (_softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || _softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
                {
                    _softwareBitmap = SoftwareBitmap.Convert(_softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }
                await _softwareBitmapSource.SetBitmapAsync(_softwareBitmap);
                CurrentFrameImage.Visibility = Visibility.Visible;
            }

            faceList = await UploadAndDetectFaces();
            if (faceList.Count > 0)
            {
                IList<Guid?> faceIds = faceList.Select(face => face.FaceId).ToList();
                // call https://westus.api.cognitive.microsoft.com/face/v1.0/persongroups to get a list of person groups
                // use the Intelligent Kiosk from the windows store to create a person group https://www.microsoft.com/store/apps/9nblggh5qd84 
                string personGroupId = "773c8334-b073-46ea-b888-fa35f32f66d2";
                var results = await faceClient.Face.IdentifyAsync(personGroupId, faceIds.Cast<Guid>().ToList());
                bool foundDracula = false;
                bool foundNigel = false;
                bool foundIndigo = false;
                bool foundGrandma = false;

                foreach (var identifyResult in results)
                {
                    var candidateArray = identifyResult.Candidates.ToArray();
                    if (candidateArray.Length > 0)
                    {
                        var candidateId = candidateArray[0].PersonId;
                        var person = await faceClient.PersonGroupPerson.GetAsync(personGroupId, candidateId);
                        if (person.Name == "Dracula") {
                            foundDracula = true;
                        }
                        else if (person.Name == "Nigel")
                        {
                            foundNigel = true;
                        }
                        else if (person.Name == "Indigo")
                        {
                            foundIndigo = true;
                        }
                        else if (person.Name == "Grandma")
                        {
                            foundGrandma = true;
                        }
                    }
                }

                String spkstr = "";
                if (faceList.Count == 1)
                {
                    if (foundDracula)
                    {
                        spkstr = "I see a person wearing a Dracula mask";

                    }
                    else if (foundNigel)
                    {
                        spkstr = spkstr = String.Format("Hi Nigel, today you look {0} years old. {2}", faceList[0].FaceAttributes.Age, faceList[0].FaceAttributes.Gender, ((faceList[0].FaceAttributes.Smile == 1) ? " smiling makes you look older" : ""));
                    }
                    else {
                        spkstr = String.Format("I see a {0} year old {1}{2}", faceList[0].FaceAttributes.Age, faceList[0].FaceAttributes.Gender, ((faceList[0].FaceAttributes.Smile == 1) ? " looking happy" : ""));
                    }
                }
                else if ((faceList.Count >= 2) && (foundDracula) && (foundNigel))
                {
                    spkstr = String.Format("Hi Nigel, why are you holding that Dracula mask?");
                }
                else if ((faceList.Count >= 2) && (foundDracula) && (!foundNigel))
                {   // hack that assumes the mask is the second face it sees I haven't written the code to match the faceList with person names yet.
                    spkstr = String.Format("I see a {0} year old {1}{2} holding a Dracula mask.", faceList[1].FaceAttributes.Age, faceList[1].FaceAttributes.Gender, ((faceList[1].FaceAttributes.Smile == 1) ? " looking happy," : ""));
                }
                else if ((faceList.Count >= 3) && (foundIndigo) && (foundGrandma) && (foundNigel))
                {
                    // hack that looks for the youngest person in the picture. I haven't written code to match people to faces yet.
                    double? minAge = 100;
                    foreach(var p in faceList)
                    {
                        if (p.FaceAttributes.Age < minAge)
                        {
                            minAge = p.FaceAttributes.Age;
                        }
                    }
                    spkstr = String.Format("Hi Nigel, that is a nice photograph of Indigo and Grandma. Indigo looks like she was {0}{1} when this was taken.", minAge, ((minAge==1)? "year old": "years old"));
                }
                CurrentFrameImage.Visibility = Visibility.Collapsed;
                await this.SpeakTextAsync(spkstr, this.uiMediaElement);
            }
        }

        private async Task<byte[]> EncodedBytes(SoftwareBitmap soft, Guid encoderId)
        {
            byte[] array = null;

            // First: Use an encoder to copy from SoftwareBitmap to an in-mem stream (FlushAsync)
            // Next:  Use ReadAsync on the in-mem stream to get byte[] array

            using (var ms = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, ms);
                encoder.SetSoftwareBitmap(soft);

                try
                {
                    await encoder.FlushAsync();
                }
                catch (Exception ex) { return new byte[0]; }

                array = new byte[ms.Size];
                await ms.ReadAsync(array.AsBuffer(), (uint)ms.Size, InputStreamOptions.None);
            }
            return array;
        }

        // Uploads the image file and calls DetectWithStreamAsync.
        private async Task<IList<DetectedFace>> UploadAndDetectFaces()
        {
            // The list of Face attributes to return.
            IList<FaceAttributeType> faceAttributes =
                new FaceAttributeType[]
                {
            FaceAttributeType.Gender, FaceAttributeType.Age,
            FaceAttributeType.Smile, FaceAttributeType.Emotion,
            FaceAttributeType.Glasses, FaceAttributeType.Hair
                };

            // Call the Face API.
            try
            {

                byte[] imageByteArray = await EncodedBytes(_softwareBitmap, BitmapEncoder.JpegEncoderId);
                MemoryStream imageFileStream = new MemoryStream(imageByteArray);

                // The second argument specifies to return the faceId, while
                // the third argument specifies not to return face landmarks.
                IList<DetectedFace> faceList =
                    await faceClient.Face.DetectWithStreamAsync(
                        imageFileStream, true, false, faceAttributes);
                return faceList;
                
            }
            // Catch and display Face API errors.
            catch (APIErrorException f)
            {
                Debug.Write(f.Message);
                return new List<DetectedFace>();
            }
            // Catch and display all other errors.
            catch (Exception e)
            {
                Debug.Write(e.Message, "Error");
                return new List<DetectedFace>();
            }
        }

        private void CameraHelper_FrameArrived(object sender, Microsoft.Toolkit.Uwp.Helpers.FrameEventArgs e)
        {
            if (e?.VideoFrame?.SoftwareBitmap == null) return;
            else
            {
                _currentVideoFrame = e.VideoFrame;
            }
        }
    }
}
