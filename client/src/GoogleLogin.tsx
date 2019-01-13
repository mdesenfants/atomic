import * as hello from 'hellojs';
import * as React from 'react';
import './GoogleLogin.css';

interface IGoogleProps {
    tokenCallback: (token: any) => void;
}

interface IGoogleState {
    tokenCallback: (token: any) => void;
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
        this.state = { tokenCallback: props.tokenCallback };
    }

    public render() {
        const callback = this.signIn.bind(this);

        return (
            <button onClick={callback}>Sign In</button>
        );
    }

    private async signIn(): Promise<void> {
        const goog = await GoogleLogin.getGoogleToken();

        if (!goog) {
            return;
        }

        this.state.tokenCallback(goog);
    }
}