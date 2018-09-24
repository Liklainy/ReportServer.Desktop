﻿using System.Configuration;
using System.Linq;
using Autofac;
using AutoMapper;
using MahApps.Metro.Controls.Dialogs;
using Monik.Client;
using Newtonsoft.Json;
using ReportServer.Desktop.Entities;
using ReportServer.Desktop.Interfaces;
using ReportServer.Desktop.Models;
using ReportServer.Desktop.ViewModels;
using ReportServer.Desktop.Views;
using ReportServer.Desktop.Views.WpfResources;
using Ui.Wpf.Common;
using Ui.Wpf.Common.ViewModels;
using MainWindow = ReportServer.Desktop.Views.MainWindow;

namespace ReportServer.Desktop
{
    public class BootsTrap : IBootstraper
    {
        public static IContainer InitContainer()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<DistinctShell>()
                .As<IShell>()
                .SingleInstance();

            builder
                .RegisterType<MainWindow>()
                .As<IDockWindow>();

            builder
                .RegisterType<CachedService>()
                .As<ICachedService>()
                .SingleInstance();

            ConfigureView<TaskManagerViewModel, TaskManagerView>(builder);

            ConfigureView<OperManagerViewModel, OperManagerView>(builder);

            ConfigureView<TaskEditorViewModel, TaskEditorView>(builder);

            ConfigureView<OperEditorViewModel, OperEditorView>(builder);

            ConfigureView<RecepientManagerViewModel, RecepientManagerView>(builder);

            ConfigureView<CronEditorViewModel, CronEditorView>(builder);

            ConfigureView<ScheduleManagerViewModel, ScheduleManagerView>(builder);

            builder.RegisterType<RecepientEditorViewModel>();

            var dialogCoordinator = DialogCoordinator.Instance;

            builder.RegisterInstance(dialogCoordinator)
                .As<IDialogCoordinator>()
                .SingleInstance();

            #region monik

            var logSender = new AzureSender(
                ConfigurationManager.AppSettings["monikendpoint"],
                "incoming");

            builder.RegisterInstance(logSender)
                .As<IClientSender>();

            var monikSettings = new ClientSettings()
            {
                SourceName = "ReportServer",
                InstanceName = ConfigurationManager.AppSettings["InstanceName"],
                AutoKeepAliveEnable = true
            };

            builder.RegisterInstance(monikSettings)
                .As<IClientSettings>();

            builder
                .RegisterType<MonikInstance>()
                .As<IClientControl>()
                .SingleInstance();

            #endregion

            #region mapper

            var mapperConfig =
                new MapperConfiguration(cfg => cfg.AddProfile(typeof(MapperProfile)));

            builder.RegisterInstance(mapperConfig)
                .As<MapperConfiguration>()
                .SingleInstance();

            builder.Register(c => c.Resolve<MapperConfiguration>()
                    .CreateMapper())
                .As<IMapper>()
                .SingleInstance();

            #endregion
            
            var container = builder.Build();
            return container;
        }

        public IShell Init()
        {
            var container = InitContainer();
            var shell = container.Resolve<IShell>();
            shell.Container = container;

            TelegramChannelsSource.Container = container;
            RecepGroupsSource.Container = container;
            return shell;
        }

        private static void ConfigureView<TViewModel, TView>(ContainerBuilder builder)
            where TViewModel : IViewModel
            where TView : IView
        {
            builder.RegisterType<TViewModel>();
            builder.RegisterType<TView>();
        }
    }

    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<ApiTask, DesktopTask>();
            CreateMap<DesktopTask, ApiTask>();

            CreateMap<ApiTaskInstance, DesktopTaskInstance>()
                .ForMember("State", opt => opt.MapFrom(s => (InstanceState) s.State));

            CreateMap<ApiOperInstance, DesktopOperInstance>()
                .ForMember("State", opt => opt.MapFrom(s => (InstanceState) s.State));


            CreateMap<TaskEditorViewModel, ApiTask>()
                .ForMember("ScheduleId", opt =>
                    opt.MapFrom(s => s.ScheduleId > 0 ? s.ScheduleId : null))
                .ForMember("BindedOpers", opt => opt.MapFrom(s =>
                    s.BindedOpers.Select(taskOper => new ApiTaskOper
                    {
                        Id = taskOper.Id,
                        OperId = taskOper.OperId,
                        Number = taskOper.Number,
                        TaskId = taskOper.TaskId,
                        IsDefault = taskOper.IsDefault
                    }).ToArray()));

            CreateMap<ApiTask, TaskEditorViewModel>();

            CreateMap<ApiRecepientGroup, RecepientEditorViewModel>();
            CreateMap<RecepientEditorViewModel, ApiRecepientGroup>();

            CreateMap<CachedService, IOperationConfig>();
            CreateMap<CachedService, EmailExporterConfig>();
            CreateMap<CachedService, TelegramExporterConfig>();

            CreateMap<ApiOper, OperEditorViewModel>();
            CreateMap<OperEditorViewModel, ApiOper>()
                .ForMember("Config", opt => opt.MapFrom
                    (s => JsonConvert.SerializeObject(s.Configuration)));
        }
    }
}