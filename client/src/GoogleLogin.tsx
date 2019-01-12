import * as hello from 'hellojs';
import * as React from 'react';
import './GoogleLogin.css';

hello.init({
    google: '1076081007580-rabmg87rit0dcdcc6m29pecc35i0lj5p.apps.googleusercontent.com'
});

class GoogleLogin extends React.Component {

    public render() {
        return (
            <button onClick={this.signIn}>Sign In</button>
        );
    }

    private signIn() {
        hello('google')
            .login({
                force: false,
                response_type: 'id_token token',
                scope: 'openid'
            })
            .then(value => {
                const token = value.authResponse ? value.authResponse.id_token : null;

                if (!token) {
                    return;
                }

                fetch("https://atomiccounter.azurewebsites.net/.auth/login/google", {
                    body: JSON.stringify({
                        "id_token": token
                    }),
                    headers: {
                        "Content-Type": "application/json"
                    },
                    method: "POST",
                }).then(response => {
                    response.json().then(ezauth => {
                        fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill", {
                            headers: {
                                "Accept": "application/json",
                                "Content-Type": "application/json",
                                "X-ZUMO-AUTH": ezauth.authenticationToken
                            },
                            method: "POST"
                        }).then(() =>
                            fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill", {
                                headers: {
                                    "Accept": "application/json",
                                    "Content-Type": "application/json",
                                    "X-ZUMO-AUTH": ezauth.authenticationToken
                                },
                                method: "GET",
                            }).then(tenant => tenant.json().then(info => {
                                const writeKey = info.writeKeys[0];
                                const readKey = info.readKeys[0];

                                fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill/app/bill/counter/bill/increment?key=" + writeKey, {
                                    headers: {
                                        "Accept": "application/json",
                                        "Content-Type": "application/json"
                                    },
                                    method: "POST"
                                }).then(() => {
                                    fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill/app/bill/counter/bill/count?key=" + readKey, {
                                        headers: {
                                            "Accept": "application/json",
                                            "Content-Type": "application/json"
                                        },
                                        method: "GET"
                                    }).then(v => v.json().then(sv => alert(sv)));
                                });
                            }))
                        );
                    });
                });
            });
    }
}

export default GoogleLogin;