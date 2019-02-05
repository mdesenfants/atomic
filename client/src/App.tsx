import * as hello from 'hellojs';
import * as React from 'react';
import './App.css';

import { AtomicCounterClient, counterNameIsValid, getAuthToken } from './atomic-counter/build/dist/atomicCounter';

import GoogleLogin from './GoogleLogin';

hello.init({
    google: '1076081007580-rabmg87rit0dcdcc6m29pecc35i0lj5p.apps.googleusercontent.com'
});

interface IAppState {
    client: AtomicCounterClient | null;
    count: number;
    timeoutHandle: NodeJS.Timeout | null;
    counterName: string;
    otherCounters: string[];
    disabled: boolean;
}

class App extends React.Component<{}, IAppState> {
    constructor(props: {}) {
        super(props);
        this.state = {
            client: null,
            count: 0,
            counterName: "",
            disabled: true,
            otherCounters: [],
            timeoutHandle: null,
        };
    }

    public componentDidMount() {
        setInterval(() => {
            if (!this.state.disabled && counterNameIsValid(this.state.counterName) || this.state.otherCounters.indexOf(this.state.counterName) > -1) {
                if (this.state.client) {
                    this.state.client.count(this.state.counterName).then(x => this.setState({ count: x }));
                }
            }
        }, 20000);
    }

    public render() {
        const callback = (value: any) => {
            const curr = new AtomicCounterClient(() => getAuthToken(value, 'google'));
            curr.getCounters().then(x => {
                this.setState({ client: curr, otherCounters: x })
            });
        };

        const selectCounter = (input: string) => {
            return () => {
                this.setState({
                    counterName: input || this.state.counterName,
                    disabled: true,
                });

                if (this.state.client) {
                    this.state.client.count(input).then(c => {
                        this.setState({
                            count: c,
                            disabled: false
                        });
                    });
                }
            };
        };

        const handle = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleCounterNameChange(evt);
        const counter = () => this.createCounter();

        const counterToLi = (input: string) =>
            <li key={input} onClick={selectCounter(input)}>
                {input}
            </li>;

        return <div>
            <div className="header clearfix">
                <h1 className="page-header">
                    Atomic Counter
                </h1>
            </div>
            <div>
                <div>
                    {this.state.client ?
                        <div className="form-group">
                            <div className="input-group">
                                <input
                                    type="text"
                                    className="form-control"
                                    value={this.state.counterName}
                                    onChange={handle}
                                    required={true}
                                    pattern="[0-9a-z]+"
                                    placeholder="Counter"
                                    maxLength={58}
                                    minLength={3}
                                />
                                <div
                                    className="input-group-append"
                                    hidden={this.state.otherCounters.indexOf(this.state.counterName) !== -1 || !counterNameIsValid(this.state.counterName)}>
                                    <button className="btn btn-success" onClick={counter}>Create Counter</button>
                                </div>
                                <div className="input-group-append" title="Select a counter" id="existing">
                                    {this.state.otherCounters.map(counterToLi)}
                                </div>
                            </div>
                        </div> : null}
                </div>
            </div>
            <div>
                <div>
                    {this.state.client ? null : <GoogleLogin tokenCallback={callback} />}
                </div>
            </div>
            <div>
                <div>
                    {this.state.client ? this.renderTools() : null}
                </div>
            </div>
        </div>;
    }

    private renderTools(): React.ReactNode {
        if (!counterNameIsValid(this.state.counterName)) {
            return "Select or create a counter to continue.";
        };

        if (this.state.disabled && this.state.otherCounters.indexOf(this.state.counterName) > -1) {
            return "Loading...";
        }

        if (counterNameIsValid(this.state.counterName) && this.state.disabled) {
            return "Create this counter to continue.";
        }

        const inc = () => this.increment();
        const reset = () => this.reset();
        const lpad = (input: number) => input.toLocaleString(undefined, { minimumIntegerDigits: 12 });

        return <div>
            <p>
                Count: {lpad(this.state.count)}
            </p>
            <hr />
            <form>
                    <label>Cost per increment</label>
                    <input type="text" pattern="[0-9]+.?[0-9]?" />
                    <br />
                    <label>Effective date</label>
                    <input type="text" pattern="[0-9]+.?[0-9]?" />
                    <button>Submit change</button>
            </form>
            <hr />
            <div title="Other actions" id="actions">
                <button onClick={inc}>Increment</button>
                <button onClick={reset}>Reset</button>
            </div>
        </div>;
    }

    private async createCounter() {
        if (this.state.client) {
            await this.state.client.createCounter(this.state.counterName);
            this.setState({ otherCounters: await this.state.client.getCounters() })
        }
    }

    private handleCounterNameChange(event: React.ChangeEvent<HTMLInputElement>) {
        this.setState({
            counterName: event.target.value.toString() || this.state.counterName,
            disabled: true,
        });

        if (this.state.client) {
            if (this.state.otherCounters.indexOf(event.target.value) === -1) {
                return;
            }

            this.state.client.count(event.target.value).then(c => {
                this.setState({
                    count: c,
                    disabled: false
                });
            });
        }
    }

    private async increment(): Promise<void> {
        if (this.state.client) {
            await this.state.client.increment(this.state.counterName);
        }
    }

    private async reset(): Promise<void> {
        if (this.state.client) {
            await this.state.client.reset(this.state.counterName);
        }
    }
}

export default App;
