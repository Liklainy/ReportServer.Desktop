﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AutoMapper;
using MahApps.Metro.Controls.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReportServer.Desktop.Entities;
using ReportServer.Desktop.Interfaces;
using ReportServer.Desktop.Models;
using ReportServer.Desktop.Views.WpfResources;
using Ui.Wpf.Common;
using Ui.Wpf.Common.ViewModels;

namespace ReportServer.Desktop.ViewModels
{
    public class OperEditorViewModel : ViewModelBase, IInitializableViewModel, ISaveableViewModel
    {
        private readonly IDialogCoordinator dialogCoordinator;
        private readonly ICachedService cachedService;
        private readonly IMapper mapper;

        public ReactiveList<string> QueryTemplates { get; set; }
        public ReactiveList<string> ViewTemplates { get; set; }

        public int? Id { get; set; }
        [Reactive] public string Name { get; set; }
        [Reactive] public string ConnectionString { get; set; }
        [Reactive] public string ViewTemplate { get; set; }
        [Reactive] public string Query { get; set; }
        [Reactive] public ReportType ReportType { get; set; }
        [Reactive] public int QueryTimeOut { get; set; } //seconds

        [Reactive] public bool IsDirty { get; set; }
        [Reactive] public bool IsValid { get; set; }

        public ReactiveCommand SaveChangesCommand { get; set; }
        public ReactiveCommand CancelCommand { get; set; }

        public OperEditorViewModel(ICachedService cachedService, IMapper mapper,
                                     IDialogCoordinator dialogCoordinator)
        {
            this.cachedService = cachedService;
            this.mapper = mapper;
            IsValid = true;
            validator = new ReportEditorValidator();
            this.dialogCoordinator = dialogCoordinator;

            var canSave = this.WhenAnyValue(tvm => tvm.IsDirty,
                isd => isd == true);

            SaveChangesCommand = ReactiveCommand.CreateFromTask(async () => await Save(),
                canSave);

            CancelCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (IsDirty)
                {
                    var dialogResult = await dialogCoordinator.ShowMessageAsync(this, "Warning",
                        "Все несохранённые изменения пропадут. Действительно закрыть окно редактирования?"
                        , MessageDialogStyle.AffirmativeAndNegative);

                    if (dialogResult != MessageDialogResult.Affirmative)
                        return;
                }

                Close();
            });

            this.WhenAnyObservable(s => s.AllErrors.Changed)
                .Subscribe(_ => IsValid = !AllErrors.Any());

            this.WhenAnyValue(s => s.ReportType)
                .Skip(2)
                .Subscribe(type =>
                {
                    Query = type == ReportType.Custom
                        ? QueryTemplates.FirstOrDefault()
                        : null;
                    ViewTemplate = type == ReportType.Custom
                        ? ViewTemplates.FirstOrDefault()
                        : null;
                    this.RaisePropertyChanged(nameof(ConnectionString));
                });

        }

        public void Initialize(ViewRequest viewRequest)
        {
            if (viewRequest is OperEditorRequest request)
            {
                FullTitle = request.FullId;
                mapper.Map(request.Oper, this);
                if (Id == 0)
                {
                    Name = "New Oper";
                    Id = null;
                    QueryTimeOut = 5;
                    ReportType = ReportType.Common;
                }
            }

            void Changed(object sender, PropertyChangedEventArgs e)
            {
                IsDirty = true;
                if (Title.Last() != '*')
                    Title += '*';
            }

            QueryTemplates = cachedService.DataImporters;
            ViewTemplates = cachedService.DataExporters;

            PropertyChanged += Changed;

            IsDirty = false;
        }

        public async Task Save()
        {
            if (!IsValid) return;

            var dialogResult = await dialogCoordinator.ShowMessageAsync(this, "Warning",
                Id > 0
                    ? "Вы действительно хотите изменить этот отчёт?"
                    : "Вы действительно хотите создать отчёт?"
                , MessageDialogStyle.AffirmativeAndNegative);

            if (dialogResult != MessageDialogResult.Affirmative) return;

            var editedReport = new ApiOper();

            if (ReportType == ReportType.Custom) ConnectionString = null;

            mapper.Map(this, editedReport);

            cachedService.CreateOrUpdateOper(editedReport);
            Close();
            cachedService.RefreshData();
        }
    }
}