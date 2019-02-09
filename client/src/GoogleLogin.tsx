import * as hello from 'hellojs';
import * as React from 'react';
import './GoogleLogin.css';

import { Button } from '@material-ui/core';

interface IGoogleProps {
    tokenCallback: (token: any) => void;
    hidden: boolean | null | undefined;
}

interface IGoogleState {
    tokenCallback: (token: any) => void;
    hidden: boolean;
}

export default class GoogleLogin extends React.Component<IGoogleProps, IGoogleState> {
    private static async getGoogleToken(): Promise<string | null> {
        const value = await hello('google').login({
            force: false,
            response_type: 'id_token token',
            scope: 'openid'
        });

        return value.authResponse ? value.authResponse.id_token ? value.authResponse.id_token : null : null;
    }

    constructor(props: IGoogleProps) {
        super(props);
        this.state = {
            hidden: props.hidden ? props.hidden : false,
            tokenCallback: props.tokenCallback,
        };
    }

    public componentDidMount() {
        const goog = sessionStorage.getItem('googleToken');
        if (goog) {
            GoogleLogin.getGoogleToken().then(x => {
                if (x) {
                    sessionStorage.setItem('googleToken', x);
                    this.state.tokenCallback(x);
                }
            });
        }
    }

    public render() {
        const callback = this.signIn.bind(this);

        return (
            <Button
                variant="text"
                hidden={this.state.hidden} onClick={callback}>
                LOGIN
            </Button>
        );
    }

    private async signIn(): Promise<void> {
        const goog = await GoogleLogin.getGoogleToken();

        if (!goog) {
            return;
        }

        sessionStorage.setItem('googleToken', goog);
        this.state.tokenCallback(goog);
    }
}