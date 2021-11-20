using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using Prism.Ioc;
using Prism.Mvvm;
using Prism.Unity;

namespace TaskTimeAmasser
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override void Initialize()
        {
            Reactive.Bindings.ReactivePropertyScheduler.SetDefault(new Reactive.Bindings.Schedulers.ReactivePropertyWpfScheduler(Dispatcher));
            base.Initialize();
        }

        protected override Window CreateShell()
        {
            var wnd = Container.Resolve<MainWindow>();
            wnd.Init();
            return wnd;
            //return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // ViewModel
            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();

            // DI
            containerRegistry.RegisterSingleton<Config.IConfig, Config.Config>();
            containerRegistry.RegisterSingleton<Repository.IRepository, Repository.Repository>();
            //containerRegistry.RegisterInstance(this.Container);


        }
    }
}
