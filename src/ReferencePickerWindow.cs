using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

internal sealed class ReferencePickerWindow : Window
{
    readonly ReferenceStore store;
    readonly TextBox query=new TextBox {Padding=new Thickness(8)},link=new TextBox {Padding=new Thickness(8)};
    readonly ListBox items=new ListBox();
    readonly TextBlock state=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)};
    readonly Dictionary<string,TextBox> fields=new Dictionary<string,TextBox>();
    readonly TabControl tabs=new TabControl();
    List<Reference> references;
    Reference fetched;
    CancellationTokenSource fetching;
    bool closed;
    public Reference Result;
    public bool Insert;
    public ReferencePickerWindow(ReferenceStore store,List<Reference> current)
    {
        this.store=store;references=current.ToList();
        Ui.Window(this,"Referenz hinzufügen");Width=Math.Min(760,SystemParameters.WorkArea.Width-32);Height=Math.Min(700,SystemParameters.WorkArea.Height-32);MinWidth=Math.Min(520,Width);MinHeight=Math.Min(400,Height);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel {Margin=new Thickness(20)};Content=root;
        var actions=new WrapPanel {Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        Ui.Button(actions,"Speichern & zitieren",delegate{Accept(true);});Ui.Button(actions,"Nur in Bibliothek speichern",delegate{Accept(false);});Ui.Button(actions,"Abbrechen",delegate{DialogResult=false;}).IsCancel=true;
        root.Children.Add(tabs);
        var existing=new DockPanel {Margin=new Thickness(12)};var libraryTab=new TabItem {Header="Aus Bibliothek",Content=existing};tabs.Items.Add(libraryTab);
        DockPanel.SetDock(query,Dock.Top);existing.Children.Add(query);query.ToolTip="Titel, Autor, Jahr oder DOI suchen";query.Margin=new Thickness(0,0,0,10);existing.Children.Add(items);
        query.TextChanged+=delegate{Filter();};items.MouseDoubleClick+=delegate{if(items.SelectedItem!=null)Accept(true);};
        var online=new DockPanel {Margin=new Thickness(12)};var onlineTab=new TabItem {Header="Aus Link / DOI",Content=online};tabs.Items.Add(onlineTab);
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);online.Children.Add(header);
        header.Children.Add(new TextBlock {Text="Link oder DOI einfügen. Nur beim Abrufen wird das Internet verwendet; dein Dokumenttext wird nicht übertragen.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});
        header.Children.Add(link);link.ToolTip="https://… oder 10.…";
        var fetchRow=new WrapPanel {Margin=new Thickness(0,8,0,0)};header.Children.Add(fetchRow);var fetchButton=Ui.Button(fetchRow,"Angaben abrufen",delegate{Fetch();});
        link.TextChanged+=delegate{if(fetching!=null)fetching.Cancel();if(fetched!=null){foreach(var box in fields.Values)box.Text="";state.Text="Link geändert. Bitte die Angaben erneut abrufen.";}fetched=null;};
        header.Children.Add(state);
        var scroll=new ScrollViewer {VerticalScrollBarVisibility=ScrollBarVisibility.Auto};online.Children.Add(scroll);var form=new StackPanel();scroll.Content=form;
        Add(form,"Title","Titel *");Add(form,"Authors","Autorinnen/Autoren – eine Person oder Organisation pro Zeile",true);Add(form,"Year","Jahr");
        var extra=new Expander {Header="Weitere Angaben",IsExpanded=false,Margin=new Thickness(0,10,0,0)};form.Children.Add(extra);var details=new StackPanel();extra.Content=details;
        foreach(var pair in new[]{new[]{"Journal","Zeitschrift / Publikation"},new[]{"Publisher","Verlag / Herausgeber"},new[]{"PublishedDate","Veröffentlichungsdatum"},new[]{"Volume","Band"},new[]{"Issue","Heft"},new[]{"Pages","Seiten"},new[]{"Doi","DOI"},new[]{"Url","URL"},new[]{"Accessed","Abrufdatum"},new[]{"Kind","Quellentyp"}})Add(details,pair[0],pair[1]);
        state.Text="Du kannst fehlende Angaben auch ohne Abruf manuell ergänzen. Es werden keine Angaben erfunden.";
        tabs.SelectedIndex=current.Count==0?1:0;Filter();
        Closed+=delegate{closed=true;if(fetching!=null)fetching.Cancel();};
    }
    void Add(Panel form,string key,string label,bool multiline=false)
    {
        form.Children.Add(new TextBlock {Text=label,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,4)});
        var box=new TextBox {Padding=new Thickness(7),AcceptsReturn=multiline,Height=multiline?65:double.NaN,VerticalScrollBarVisibility=multiline?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden};fields[key]=box;form.Children.Add(box);
    }
    void Filter()
    {
        items.Items.Clear();foreach(var reference in references.Where(x=>x.Bibliography.IndexOf(query.Text,StringComparison.OrdinalIgnoreCase)>=0))items.Items.Add(new ListBoxItem {Tag=reference,Content=new TextBlock {Text=reference.ToString(),TextWrapping=TextWrapping.Wrap,MaxWidth=640,Margin=new Thickness(4,8,4,8)}});
    }
    async void Fetch()
    {
        if(fetching!=null)fetching.Cancel();var cancel=new CancellationTokenSource();fetching=cancel;string input=link.Text.Trim();
        foreach(var box in fields.Values)box.Text="";state.Text="Bibliografische Angaben werden abgerufen …";
        try
        {
            var reference=await ReferenceLookup.Fetch(input,cancel.Token);if(closed || input!=link.Text.Trim() || fetching!=cancel)return;
            fetched=reference;Set(reference);state.Text=(string.IsNullOrWhiteSpace(reference.Title)?"Kein Titel gefunden. Bitte ergänzen.":"Gefunden: "+reference.MetadataSource+". Bitte vor dem Speichern prüfen.")+(reference.Authors.Count==0?" Autoren fehlen.":"")+(string.IsNullOrWhiteSpace(reference.Year)?" Veröffentlichungsjahr fehlt.":"");
        }
        catch(OperationCanceledException){if(!closed && fetching==cancel)state.Text="Abruf abgebrochen oder Zeitlimit erreicht. Du kannst Angaben manuell ergänzen.";}
        catch(Exception ex){if(!closed && fetching==cancel){state.Text="Abruf nicht möglich: "+ex.Message+" Angaben können manuell eingetragen werden.";string doi=ReferenceLookup.Doi(input);fields["Url"].Text=doi==""?input:"https://doi.org/"+doi;fields["Doi"].Text=doi;}}
        finally{cancel.Dispose();if(fetching==cancel)fetching=null;}
    }
    void Set(Reference value)
    {
        fields["Title"].Text=value.Title??"";fields["Authors"].Text=string.Join(Environment.NewLine,value.Authors);fields["Year"].Text=value.Year??"";
        foreach(string key in new[]{"Journal","Publisher","PublishedDate","Volume","Issue","Pages","Doi","Url","Accessed","Kind"})fields[key].Text=Convert.ToString(typeof(Reference).GetField(key).GetValue(value));
    }
    void Accept(bool insert)
    {
        try
        {
            if(fetching!=null){MessageBox.Show("Bitte den Abruf abwarten oder abbrechen.","Referenz hinzufügen");return;}
            Reference reference;
            if(tabs.SelectedIndex==0){var item=items.SelectedItem as ListBoxItem;if(item==null){MessageBox.Show("Bitte eine Quelle auswählen.");return;}reference=(Reference)item.Tag;}
            else
            {
                reference=fetched??new Reference();reference.Title=fields["Title"].Text.Trim();reference.Year=fields["Year"].Text.Trim();reference.Authors=fields["Authors"].Text.Split('\n').Select(x=>x.Trim()).Where(x=>x!="").ToList();
                foreach(string key in new[]{"Journal","Publisher","PublishedDate","Volume","Issue","Pages","Doi","Url","Accessed","Kind"})typeof(Reference).GetField(key).SetValue(reference,fields[key].Text.Trim());
                if(string.IsNullOrWhiteSpace(reference.Title)){MessageBox.Show("Bitte den Titel ergänzen. Fehlende Autoren oder Jahreszahlen dürfen leer bleiben.");return;}
                if(string.IsNullOrWhiteSpace(reference.Url) && ReferenceLookup.Doi(link.Text)=="")reference.Url=link.Text.Trim();
                if(string.IsNullOrWhiteSpace(reference.Doi))reference.Doi=ReferenceLookup.Doi(link.Text);
                if(!string.IsNullOrWhiteSpace(reference.Url))ReferenceLookup.PublicUrl(reference.Url);
                if(string.IsNullOrWhiteSpace(reference.Accessed))reference.Accessed=DateTime.Now.ToString("yyyy-MM-dd");
                if(string.IsNullOrWhiteSpace(reference.MetadataSource))reference.MetadataSource="Manuell ergänzt";
                reference.Source=link.Text.Trim();reference=store.Upsert(reference);
            }
            Result=reference;Insert=insert;DialogResult=true;
        }
        catch(Exception ex){MessageBox.Show("Quelle konnte nicht gespeichert werden: "+ex.Message,"Referenz hinzufügen");}
    }
}
