using System;
namespace Server.ViewModels
{
    public class LoginViewModel
    {
		public LoginViewModel(string returnUrl): this(returnUrl, false) {
        }
        
        public LoginViewModel(string returnUrl, bool loginRejected) {
			ReturnUrl = returnUrl;
            LoginRejected = loginRejected;
        }

		public string ReturnUrl { get; }

        public bool LoginRejected { get; }
    }
}
