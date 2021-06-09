using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Template10.Common;
using Template10.Mvvm;
using Template10.Services.NavigationService;
using Template10.Utils;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Words.Models;
using Words.Services;
using Words.Views;

namespace Words.ViewModels
{
    // A kezd�oldalhoz tartoz� ViewModel.
    // Feladata, hogy kommunik�ljon a ford�t� service-el (YandexDictionaryService)
    // �s a kapott adatokat a View-nak szolg�ltassa.
    // Emellett lehet�s�get biztos�t a szinon�ma keres� n�zetre val� navig�l�shoz.
    public class MainPageViewModel : ViewModelBase
    {
        // Konstruktor, amely inicializ�lja az esem�nykezel�ket �s a DelegateCommandot
        public MainPageViewModel()
        {
            LangFromChanged = new SelectionChangedEventHandler(UpdateLangsTo);
            LangToChanged = new SelectionChangedEventHandler(LanguageToChanged);
            TranslateCommand = new DelegateCommand(Translate, CanExecute);
        }

        private List<string> Langs { get; set; } = new List<string>();

        // A forr�snyelvek list�ja.
        // Csak az oldalra navig�l�skor v�ltoznak az elemei.
        public ObservableCollection<string> LangsFrom { get; set; } =
            new ObservableCollection<string>();

        // A c�lnyelvek list�ja.
        // Amikor kiv�lasztjuk a forr�snyelvet,
        // a c�lnyelvek elemeit friss�tj�k aszerint,
        // hogy el�rhet�ek-e az adott forr�snyelvhez.
        public ObservableCollection<string> LangsTo { get; set; } =
            new ObservableCollection<string>();

        // A szervert�l kapott leford�tott elemek.
        // Minden Translation objektumhoz tartozik:
        // Leford�tott sz�, t�pus �s az eredeti nyelven jelent�se a sz�nak a forr�snyelven
        public ObservableCollection<Translation> Translations { get; set; } =
            new ObservableCollection<Translation>();

        private string _word = "";
        // A felhaszn�l� �ltal beg�pelt sz�hoz tatoz� property
        public string Word
        {
            get { return _word; }
            set
            {
                _word = value;
                TranslateCommand.RaiseCanExecuteChanged();    
            }
        }

        private string from;
        // A felhaszn�l� �ltal kiv�lasztott forr�snyelvhez tatoz� property
        public string From
        {
            get { return from; }
            set { from = value; }
        }

        private string to;
        // A felhaszn�l� �ltal kiv�lasztott c�lnyelvhez tatoz� property
        public string To
        {
            get { return to; }
            set { to = value; }
        }

        // A kezd�lapra navig�l�skor v�grehajt�d� f�ggv�ny
        // Elk�ri �s elmenti a szervert�l a kiv�laszthat� nyelvp�rok list�j�t �s
        // ez alapj�n felt�lti a forr�snyelvek list�j�t.
        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            try
            {
                var service = new YandexDictionaryService();
                var list = await service.GetLangsAsync();
                List<string> from = new List<string>();
                foreach (var lang in list)
                {
                    Langs.Add(lang);
                    string[] pair = lang.Split('-');
                    from.Add(pair[0]);
                }
                LangsFrom.AddRange(from.Distinct());
            }
            catch (HttpRequestException)
            {
                await DisplayDialog("Something went worng", "Please check your internet connection!");
                Application.Current.Exit();
            }
            await base.OnNavigatedToAsync(parameter, mode, state);
        }

        // A forr�snyelv megv�ltoz�s�hoz tartoz� esem�nykezel� property
        public SelectionChangedEventHandler LangFromChanged { get; }
        // A c�lnyelv megv�ltoz�s�hoz tartoz� esem�nykezel� property
        public SelectionChangedEventHandler LangToChanged { get; }
        // A ford�t�shoz tartoz� Command property
        public DelegateCommand TranslateCommand { get; }

        private void UpdateLangsTo(object sender, SelectionChangedEventArgs e)
        {
            string fromItem = (sender as ComboBox).SelectedItem.ToString();
            LangsTo.Clear();
            foreach (var item in Langs)
            {
                string[] langPair = item.Split('-');
                if (fromItem == langPair[0] && langPair[0] != langPair[1] && !LangsTo.Contains(langPair[1]))
                    LangsTo.Add(langPair[1]);
            }
            TranslateCommand.RaiseCanExecuteChanged();
        }

        private async void Translate()
        {
            Translations.Clear();
            try
            {
                var service = new YandexDictionaryService();
                var translation = await service.GetTranslationAsync(Word.Trim(), $"{From}-{To}");
                if (translation.Def.Length == 0)
                    await DisplayDialog("Couldn't find translation", "This word was not found in our database.\nPlease try a different word or language.");
                else
                    foreach (var def in translation.Def)
                        foreach (var tr in def.Tr)
                        {
                            string meanings = "";
                            if (tr.Mean != null)
                            {
                                for (int i = 0; i < tr.Mean.Length - 1; i++)
                                    meanings += $"{tr.Mean[i].Text}, ";
                                meanings += tr.Mean[tr.Mean.Length - 1].Text;
                            }
                            Translations.Add(new Translation() { TranslatedWord = tr.Text, WordType = tr.Pos, Meaning = meanings });
                        }
            }
            catch (HttpRequestException)
            {
                await DisplayDialog("Something went worng", "Please check your internet connection!");
            }
        }

        private bool CanExecute() => !string.IsNullOrWhiteSpace(From) && !string.IsNullOrWhiteSpace(To) && !string.IsNullOrWhiteSpace(Word);

        private void LanguageToChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox).SelectedIndex != -1) {
                TranslateCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task DisplayDialog(string title, string content)
        {
            ContentDialog noResultDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "Ok"
            };

            await noResultDialog.ShowAsync();
        }

        // Az szinonim�k oldal�ra navig�l� f�ggv�ny
        // Param�terben �tk�ldi a felhaszn�l� �ltal beg�pelt sz�t
        public void NavigateToSynonyms(object sender, Windows.UI.Xaml.RoutedEventArgs e) => NavigationService.Navigate(typeof(SynonimPage), _word.Trim());
    }
}

