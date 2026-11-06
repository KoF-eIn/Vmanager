using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace VodokanalManager.Views
{
    public partial class ReceiptPreviewWindow : Window
    {
        private readonly FlowDocument _document;

        public ReceiptPreviewWindow(FlowDocument document)
        {
            InitializeComponent();
            _document = document ?? throw new ArgumentNullException(nameof(document));
            ReceiptViewer.Document = _document;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var paginator = ((IDocumentPaginatorSource)_document).DocumentPaginator;
                printDialog.PrintDocument(paginator, "Квитанция МУП Водоканал г. Канск");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
