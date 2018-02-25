using System;
namespace Server.ViewModels
{
    public class LoginViewModel
    {
        public LoginViewModel(): this(false) {
        }
        
        public LoginViewModel(bool loginRejected) {
            LoginRejected = loginRejected;
        }

        public bool LoginRejected { get; }
    }
}
