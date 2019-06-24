import * as React from "react";
import "./StripeLogin.css";

import { Button } from "@material-ui/core";

interface IStripeProps {
    tokenCallback: (token: any) => void;
}

interface IStripeState {
    tokenCallback: (token: any) => void;
}

const tokenLocation = "stripe_token";

export default class StripeLogin extends React.Component<IStripeProps, IStripeState> {
    constructor(props: IStripeProps) {
        super(props);

        this.state = {
            tokenCallback: props.tokenCallback,
        };
    }

    public render() {
        const callback = this.signIn.bind(this);

        const token = window.localStorage.getItem(tokenLocation);

        if (window.location.search) {
            const query = new URLSearchParams(window.location.search);
            window.localStorage.setItem(tokenLocation, query.get("code") || "");
            window.location.replace("/");
        } else if (token) {
            this.state.tokenCallback(token);
        }

        const logout = (): any => {
            window.localStorage.removeItem(tokenLocation);
        };

        return (
            <Button
                variant="text"
                onClick={token ? logout : callback}>
                {token ? "LOGOUT" : "LOGIN"}
            </Button>
        );
    }

    private async signIn(): Promise<void> {
        const clientId = encodeURIComponent("ca_EVLDOec67UuKVBalXSJId6dsZFPfcQMr");
        const redirect = encodeURIComponent("http://localhost:3000/");
        const scope = encodeURIComponent("read_write");

        // tslint:disable-next-line:max-line-length
        window.location.replace(`https://connect.stripe.com/oauth/authorize?client_id=${clientId}&redirect_uri=${redirect}&response_type=code&scope=${scope}`);
    }
}
