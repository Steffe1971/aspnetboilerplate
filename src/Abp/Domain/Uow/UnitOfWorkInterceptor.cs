using Abp.Dependency;
using Castle.DynamicProxy;

namespace Abp.Domain.Uow
{
    /// <summary>
    /// This interceptor is used to manage database connection and transactions.
    /// </summary>
    internal class UnitOfWorkInterceptor : IInterceptor
    {
        private readonly IIocResolver _iocResolver;

        public UnitOfWorkInterceptor(IIocResolver iocResolver)
        {
            _iocResolver = iocResolver;
        }

        /// <summary>
        /// Intercepts a method.
        /// </summary>
        /// <param name="invocation">Method invocation arguments</param>
        public void Intercept(IInvocation invocation)
        {
            var unitOfWorkAttr = UnitOfWorkAttribute.GetUnitOfWorkAttributeOrDefault(invocation.MethodInvocationTarget);
            if (unitOfWorkAttr == null || unitOfWorkAttr.IsDisabled)
            {
                //No need to a uow
                invocation.Proceed();
                return;
            }

            if (UnitOfWorkScope.Current == null)
            {
                //No current uow, run a new one
                PerformUow(invocation, unitOfWorkAttr.IsTransactional != false);
            }
            else
            {
                //Continue with current uow
                invocation.Proceed();
            }
        }

        private void PerformUow(IInvocation invocation, bool isTransactional)
        {
            using (var unitOfWork = _iocResolver.ResolveAsDisposable<IUnitOfWork>())
            {
                try
                {
                    UnitOfWorkScope.Current = unitOfWork.Object;
                    UnitOfWorkScope.Current.Initialize(isTransactional);
                    UnitOfWorkScope.Current.Begin();

                    try
                    {
                        invocation.Proceed();
                        UnitOfWorkScope.Current.End();
                    }
                    catch
                    {
                        try { UnitOfWorkScope.Current.Cancel(); } catch { } //Hide exceptions on cancelling
                        throw;
                    }
                }
                finally
                {
                    UnitOfWorkScope.Current = null;
                }
            }
        }
    }
}