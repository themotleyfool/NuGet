using System;
using System.Management.Automation;
using EnvDTE;
using NuGet.VisualStudio;

namespace NuGet.PowerShell.Commands
{
    /// <summary>
    /// This project updates the specified package to the specified project.
    /// </summary>
    [Cmdlet(VerbsData.Update, "Package", DefaultParameterSetName = "All")]
    public class UpdatePackageCommand : ProcessPackageBaseCommand, IPackageOperationEventListener
    {
        private readonly IVsPackageSourceProvider _packageSourceProvider;
        private readonly IPackageRepositoryFactory _repositoryFactory;
        private readonly IProductUpdateService _productUpdateService;
        private bool _hasConnectedToHttpSource;

        public UpdatePackageCommand()
            : this(ServiceLocator.GetInstance<ISolutionManager>(),
                   ServiceLocator.GetInstance<IVsPackageManagerFactory>(),
                   ServiceLocator.GetInstance<IPackageRepositoryFactory>(),
                   ServiceLocator.GetInstance<IVsPackageSourceProvider>(),
                   ServiceLocator.GetInstance<IHttpClientEvents>(),
                   ServiceLocator.GetInstance<IProductUpdateService>(),
                   ServiceLocator.GetInstance<IVsCommonOperations>())
        {
        }

        public UpdatePackageCommand(ISolutionManager solutionManager,
                                    IVsPackageManagerFactory packageManagerFactory,
                                    IPackageRepositoryFactory repositoryFactory,
                                    IVsPackageSourceProvider packageSourceProvider,
                                    IHttpClientEvents httpClientEvents,
                                    IProductUpdateService productUpdateService,
                                    IVsCommonOperations vsCommonOperations)
            : base(solutionManager, packageManagerFactory, httpClientEvents, vsCommonOperations)
        {
            _repositoryFactory = repositoryFactory;
            _packageSourceProvider = packageSourceProvider;
            _productUpdateService = productUpdateService;
        }

        // We need to override id since it's mandatory in the base class. We don't
        // want it to be mandatory here.
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "Project")]
        [Parameter(ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "All")]
        public override string Id
        {
            get
            {
                return base.Id;
            }
            set
            {
                base.Id = value;
            }
        }

        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "All")]
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "Project")]
        public override string ProjectName
        {
            get
            {
                return base.ProjectName;
            }
            set
            {
                base.ProjectName = value;
            }
        }

        [Parameter(Position = 2, ParameterSetName = "Project")]
        [ValidateNotNull]
        public SemanticVersion Version { get; set; }

        [Parameter(Position = 3)]
        [ValidateNotNullOrEmpty]
        public string Source { get; set; }

        [Parameter]
        public SwitchParameter IgnoreDependencies { get; set; }

        [Parameter]
        public SwitchParameter Safe { get; set; }

        [Parameter]
        public SwitchParameter Minor { get; set; }

        [Parameter, Alias("Prerelease")]
        public SwitchParameter IncludePrerelease { get; set; }

        protected override IVsPackageManager CreatePackageManager()
        {
            if (!String.IsNullOrEmpty(Source))
            {
                IPackageRepository repository = CreateRepositoryFromSource(_repositoryFactory, _packageSourceProvider, Source);
                return repository == null ? null : PackageManagerFactory.CreatePackageManager(repository, useFallbackForDependencies: true);
            }
            return base.CreatePackageManager();
        }

        protected override void ProcessRecordCore()
        {
            if (!SolutionManager.IsSolutionOpen)
            {
                // terminating
                ErrorHandler.ThrowSolutionNotOpenTerminatingError();
            }

            if (PackageManager == null) return;

            SubscribeToProgressEvents();

            try
            {
                IProjectManager projectManager = ProjectManager;
                if (!String.IsNullOrEmpty(Id))
                {
                    // If a package id was specified, but no project was specified, then update this package in all projects
                    if (String.IsNullOrEmpty(ProjectName))
                    {
                        if (Version != null)
                        {
                            PackageManager.UpdatePackageToSpecificVersion(Id, Version, !IgnoreDependencies.IsPresent,
                                                                          IncludePrerelease, this, this);
                        }
                        else
                        {
                            PackageManager.UpdatePackage(Id, UpdateMode, !IgnoreDependencies.IsPresent,
                                                         IncludePrerelease, this, this);
                        }
                    }
                    else if (projectManager != null)
                    {
                        // If there was a project specified, then update the package in that project
                        if (Version != null)
                        {
                            PackageManager.UpdatePackageToSpecificVersion(projectManager, Id, Version,
                                                                          !IgnoreDependencies, IncludePrerelease, this);
                        }
                        else
                        {
                            PackageManager.UpdatePackage(projectManager, Id, UpdateMode, !IgnoreDependencies,
                                                         IncludePrerelease, this);
                        }
                    }
                }
                else
                {
                    if (String.IsNullOrEmpty(ProjectName))
                    {
                        PackageManager.UpdatePackages(UpdateMode, !IgnoreDependencies.IsPresent, IncludePrerelease, this, this);
                    }
                    else if (projectManager != null)
                    {
                        PackageManager.UpdatePackages(projectManager, UpdateMode, !IgnoreDependencies.IsPresent, IncludePrerelease, this);
                    }
                }
                _hasConnectedToHttpSource |= UriHelper.IsHttpSource(Source, _packageSourceProvider);
            }
            finally
            {
                UnsubscribeFromProgressEvents();
            }
        }

        protected PackageUpdateMode UpdateMode
        {
            get
            {
                if (Safe) return PackageUpdateMode.Safe;
                if (Minor) return PackageUpdateMode.Minor;
                return PackageUpdateMode.Newest;
            }
        }

        protected override void EndProcessing()
        {
            base.EndProcessing();

            CheckForNuGetUpdate();
        }

        private void CheckForNuGetUpdate()
        {
            if (_productUpdateService != null && _hasConnectedToHttpSource)
            {
                _productUpdateService.CheckForAvailableUpdateAsync();
            }
        }

        public void OnBeforeAddPackageReference(Project project)
        {
            RegisterProjectEvents(project);
        }

        public void OnAfterAddPackageReference(Project project)
        {
            // No-op
        }

        public void OnAddPackageReferenceError(Project project, Exception exception)
        {
            // No-op
        }
    }
}