import * as React from 'react';
import './GoogleLogin.css';

import { Button } from '@material-ui/core';

interface IGoogleProps {
    tokenCallback: (token: any) => void;
}

interface IGoogleState {
    tokenCallback: (token: any) => void;
}

const tokenLocation = 'strip_token';

export default class GoogleLogin extends React.Component<IGoogleProps, IGoogleState> {
    constructor(props: IGoogleProps) {
        super(props);

        this.state = {
            tokenCallback: props.tokenCallback,
        };
    }

    public render() {
        const callback = this.signIn.bind(this);

        if (window.location.search) {
            const query = new URLSearchParams(window.location.search);
            window.localStorage.setItem(tokenLocation, query.get('code') || '');
            window.location.replace('/');
        } else {
            const token = window.localStorage.getItem(tokenLocation);

            if (token && token !== "") {
                this.state.tokenCallback(token);
            }
        }

        return (
            <Button
                variant="text"
                onClick={callback}>
                LOGIN
            </Button>
        );
    }

    private async signIn(): Promise<void> {
        const clientId = encodeURIComponent('ca_EVLDOec67UuKVBalXSJId6dsZFPfcQMr');
        const redirect = encodeURIComponent('http://localhost:3000/');
        const scope = encodeURIComponent('read_write');

        window.location.replace(`https://connect.stripe.com/oauth/authorize?client_id=${clientId}&redirect_uri=${redirect}&response_type=code&scope=${scope}`);
    }
}